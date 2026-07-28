using CodeTutor.Application.Ai;
using CodeTutor.Domain.Common;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Sessions;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Application.Abstractions;

public interface ICameraService : IAsyncDisposable
{
    Task<IReadOnlyList<CameraDescriptor>> EnumerateAsync(CancellationToken ct);
    Task StartAsync(CameraSelection selection, Func<CameraFrame, ValueTask> onFrame, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    CameraFrame? TryGetLatestFrameCopy();
}

public sealed record CameraDescriptor(
    string Id,
    string Name,
    IReadOnlyList<VideoMode> Modes);

public sealed record VideoMode(
    int Width,
    int Height,
    int Fps,
    string PixelFormat,
    int Score);

public sealed record CameraSelection(
    string DeviceId,
    VideoMode Mode);

public sealed record CameraFrame(
    byte[] Data,
    int Width,
    int Height,
    string PixelFormat,
    DateTimeOffset CapturedAt);

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(Stream image, OcrRequestOptions options, CancellationToken ct);
    Task<bool> HealthCheckAsync(CancellationToken ct);
}

public interface IQuestionTextMerger
{
    MergeResult Merge(string existingText, OcrResult incoming);
}

public interface ITextAnswerProvider
{
    Task<SolutionResult> SolveAsync(string questionText, CancellationToken ct);
    Task<string> FollowUpAsync(string questionText, SolutionResult solution, string message, CancellationToken ct);
    Task<ProviderTestResult> TestAsync(CancellationToken ct);
    Task<BalanceInfo?> GetBalanceAsync(CancellationToken ct);
}

public interface IVisionAnswerProvider
{
    bool IsConfigured { get; }
    Task<SolutionResult> SolveFromImagesAsync(IReadOnlyList<string> imagePaths, CancellationToken ct);
    Task<ProviderTestResult> TestAsync(CancellationToken ct);
}

public interface ISessionRepository
{
    Task<StudySession> CreateAsync(CancellationToken ct);
    Task SaveAsync(StudySession session, CancellationToken ct);
    Task<StudySession?> GetAsync(Guid id, CancellationToken ct);
    Task<StudySession?> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<SessionSummary>> GetRecentAsync(int limit, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task DeleteAllAsync(CancellationToken ct);
}

public interface ISecretStore
{
    Task SaveAsync(string name, string value, CancellationToken ct);
    Task<string?> GetAsync(string name, CancellationToken ct);
    Task DeleteAsync(string name, CancellationToken ct);
}

public interface IImageStore
{
    Task<(string ImagePath, string ThumbnailPath)> SaveCaptureAsync(
        Guid sessionId,
        int sequence,
        Stream image,
        CancellationToken ct);
    Task MoveToTrashAsync(string imagePath, CancellationToken ct);
}

public interface ICheckpointStore
{
    Task PushAsync(SessionCheckpoint checkpoint, CancellationToken ct);
    Task<SessionCheckpoint?> PopAsync(Guid sessionId, CancellationToken ct);
    Task<bool> HasAnyAsync(Guid sessionId, CancellationToken ct);
}
