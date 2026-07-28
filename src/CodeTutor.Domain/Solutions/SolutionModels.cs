namespace CodeTutor.Domain.Solutions;

public enum QuestionType
{
    Unknown = 0,
    Choice = 1,
    Fill = 2,
    Programming = 3
}

public sealed record SolutionResult(
    QuestionType QuestionType,
    string FinalAnswer,
    string Explanation,
    string Code,
    string ProgrammingLanguage,
    bool NeedsMoreContext,
    double Confidence,
    string Provider,
    string Model,
    DateTimeOffset CreatedAt);

public sealed record ProviderTestResult(
    bool Success,
    string Message,
    TimeSpan Elapsed);
