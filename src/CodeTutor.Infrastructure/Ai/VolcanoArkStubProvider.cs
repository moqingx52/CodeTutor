using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Infrastructure.Ai;

public sealed class VolcanoArkStubProvider : ITextAnswerProvider
{
    private const string NotSupportedMessage = "火山方舟即将支持，当前请切换为 DeepSeek。";

    public Task<SolutionResult> SolveAsync(string questionText, CancellationToken ct) =>
        throw new InvalidOperationException(NotSupportedMessage);

    public Task<string> FollowUpAsync(
        string questionText,
        SolutionResult solution,
        string message,
        CancellationToken ct) =>
        throw new InvalidOperationException(NotSupportedMessage);

    public Task<ProviderTestResult> TestAsync(CancellationToken ct) =>
        throw new InvalidOperationException(NotSupportedMessage);

    public Task<BalanceInfo?> GetBalanceAsync(CancellationToken ct) =>
        Task.FromResult<BalanceInfo?>(null);
}
