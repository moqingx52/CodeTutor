namespace CodeTutor.Domain.Common;

public enum FeedbackMessageType
{
    SystemInfo = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    User = 4,
    Assistant = 5
}

public sealed record ChatMessage(
    Guid Id,
    FeedbackMessageType Type,
    string Content,
    DateTimeOffset CreatedAt);

public enum AppOperationState
{
    NoCamera = 0,
    Previewing = 1,
    OcrProcessing = 2,
    Ready = 3,
    SolvingText = 4,
    SolvingVision = 5,
    Cancelling = 6,
    Error = 7
}
