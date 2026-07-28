using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Domain.Sessions;

namespace CodeTutor.Application.UseCases;

public sealed class UndoLastCaptureUseCase : IUndoLastCaptureUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ICheckpointStore _checkpoints;
    private readonly IImageStore _imageStore;

    public UndoLastCaptureUseCase(
        IAppSessionContext session,
        ICheckpointStore checkpoints,
        IImageStore imageStore)
    {
        _session = session;
        _checkpoints = checkpoints;
        _imageStore = imageStore;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var session = _session.Current;
        var checkpoint = await _checkpoints.PopAsync(session.Id, ct)
                         ?? throw new InvalidOperationException("没有可撤销的截屏记录。");

        var capturesToRemove = session.Captures
            .Where(c => c.Sequence > checkpoint.CaptureCount)
            .ToList();

        foreach (var capture in capturesToRemove)
            await _imageStore.MoveToTrashAsync(capture.ImagePath, ct);

        var remaining = session.Captures
            .Where(c => c.Sequence <= checkpoint.CaptureCount)
            .ToList();

        var restored = session with
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            WorkingQuestionText = checkpoint.WorkingQuestionText,
            IsQuestionTextManuallyEdited = checkpoint.IsQuestionTextManuallyEdited,
            Captures = remaining,
            Solution = checkpoint.Solution,
            ChatMessages = checkpoint.ChatMessages.ToList()
        };

        _session.ApplySession(restored);
        await _session.PersistCurrentAsync(ct);
    }
}
