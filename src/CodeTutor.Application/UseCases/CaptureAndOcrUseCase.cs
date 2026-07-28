using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.UseCases;

public sealed class CaptureAndOcrUseCase : ICaptureAndOcrUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ICameraService _camera;
    private readonly IImageStore _imageStore;
    private readonly IOcrService _ocr;
    private readonly IQuestionTextMerger _merger;
    private readonly ICheckpointStore _checkpoints;

    public CaptureAndOcrUseCase(
        IAppSessionContext session,
        ICameraService camera,
        IImageStore imageStore,
        IOcrService ocr,
        IQuestionTextMerger merger,
        ICheckpointStore checkpoints)
    {
        _session = session;
        _camera = camera;
        _imageStore = imageStore;
        _ocr = ocr;
        _merger = merger;
        _checkpoints = checkpoints;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var frame = _camera.TryGetLatestFrameCopy()
                    ?? throw new InvalidOperationException("没有可用的相机帧，请确认预览已启动。");

        var session = _session.Current;
        await _checkpoints.PushAsync(new SessionCheckpoint(
            session.Id,
            session.Captures.Count,
            session.WorkingQuestionText,
            session.IsQuestionTextManuallyEdited,
            session.Solution,
            session.ChatMessages,
            DateTimeOffset.UtcNow), ct);

        var sequence = session.Captures.Count + 1;
        await using var imageStream = new MemoryStream(frame.Data);
        var (imagePath, thumbPath) = await _imageStore.SaveCaptureAsync(session.Id, sequence, imageStream, ct);

        var pHash = ComputePerceptualHash(frame.Data);
        if (session.Captures.Any(c => c.PerceptualHash == pHash))
        {
            await _checkpoints.PopAsync(session.Id, ct);
            await _imageStore.MoveToTrashAsync(imagePath, ct);
            throw new DuplicateCaptureException("检测到重复截图，未追加文字。");
        }

        OcrResult? ocrResult = null;
        MergeDecision? mergeDecision = null;
        var ocrStatus = OcrStatus.Pending;
        string? errorMessage = null;
        var workingText = session.WorkingQuestionText;

        try
        {
            await using var ocrStream = new MemoryStream(frame.Data);
            ocrResult = await _ocr.RecognizeAsync(
                ocrStream,
                new OcrRequestOptions(RequestId: Guid.NewGuid().ToString("N")),
                ct);

            var merge = _merger.Merge(workingText, ocrResult);
            workingText = merge.MergedText;
            mergeDecision = merge.Decision;
            ocrStatus = OcrStatus.Succeeded;
        }
        catch (Exception ex)
        {
            ocrStatus = OcrStatus.Failed;
            errorMessage = ex.Message;
        }

        var capture = new CaptureRecord(
            Guid.NewGuid(),
            session.Id,
            sequence,
            DateTimeOffset.UtcNow,
            imagePath,
            thumbPath,
            pHash,
            ocrStatus,
            ocrResult,
            mergeDecision,
            errorMessage);

        var updated = session with
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            WorkingQuestionText = workingText,
            IsQuestionTextManuallyEdited = false,
            Captures = session.Captures.Append(capture).ToList(),
            Solution = null
        };

        _session.ApplySession(updated);
        await _session.PersistCurrentAsync(ct);
    }

    private static string ComputePerceptualHash(byte[] data)
    {
        // 简单哈希占位；Infrastructure 层在后续可注入专用服务。
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexString(hash)[..32];
    }
}

public sealed class DuplicateCaptureException : Exception
{
    public DuplicateCaptureException(string message) : base(message) { }
}
