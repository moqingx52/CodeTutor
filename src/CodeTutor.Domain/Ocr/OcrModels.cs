namespace CodeTutor.Domain.Ocr;

public sealed record OcrPoint(float X, float Y);

public sealed record OcrResult(
    string FullText,
    double MeanConfidence,
    TimeSpan Elapsed,
    IReadOnlyList<OcrLine> Lines);

public sealed record OcrLine(
    string Text,
    double Confidence,
    IReadOnlyList<OcrPoint> Polygon);

public sealed record OcrRequestOptions(
    string Profile = "screen-default",
    string Language = "ch_en",
    string? RequestId = null);

public sealed record MergeDecision(
    MergeStrategy Strategy,
    int OverlapLineCount,
    int OverlapCharCount,
    double Score,
    bool IsReliable);

public enum MergeStrategy
{
    First = 0,
    LineOverlap = 1,
    CharacterOverlap = 2,
    NoOverlapWithWarning = 3,
    DuplicateSkipped = 4
}

public sealed record MergeResult(
    string MergedText,
    MergeDecision Decision);
