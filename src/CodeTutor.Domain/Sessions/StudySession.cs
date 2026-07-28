namespace CodeTutor.Domain.Sessions;

using CodeTutor.Domain.Common;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Solutions;

public enum SessionStatus
{
    Active = 0,
    Archived = 1
}

public enum OcrStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Duplicate = 3
}

public sealed record StudySession(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SessionStatus Status,
    string WorkingQuestionText,
    bool IsQuestionTextManuallyEdited,
    IReadOnlyList<CaptureRecord> Captures,
    SolutionResult? Solution,
    IReadOnlyList<ChatMessage> ChatMessages);

public sealed record CaptureRecord(
    Guid Id,
    Guid SessionId,
    int Sequence,
    DateTimeOffset CapturedAt,
    string ImagePath,
    string ThumbnailPath,
    string PerceptualHash,
    OcrStatus OcrStatus,
    OcrResult? Ocr,
    MergeDecision? MergeDecision,
    string? ErrorMessage);

public sealed record SessionCheckpoint(
    Guid SessionId,
    int CaptureCount,
    string WorkingQuestionText,
    bool IsQuestionTextManuallyEdited,
    SolutionResult? Solution,
    IReadOnlyList<ChatMessage> ChatMessages,
    DateTimeOffset CreatedAt);

public sealed record SessionSummary(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CaptureCount,
    string PreviewText);
