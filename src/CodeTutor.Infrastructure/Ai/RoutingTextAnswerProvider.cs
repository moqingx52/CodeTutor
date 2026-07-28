using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Infrastructure.Ai;

public sealed class RoutingTextAnswerProvider : ITextAnswerProvider
{
    private readonly ISecretStore _secrets;
    private readonly DeepSeekTextAnswerProvider _deepSeek;
    private readonly VolcanoArkStubProvider _volcanoArk;
    private readonly DeepSeekApiCallTracker _tracker;

    public RoutingTextAnswerProvider(
        ISecretStore secrets,
        DeepSeekTextAnswerProvider deepSeek,
        VolcanoArkStubProvider volcanoArk,
        DeepSeekApiCallTracker tracker)
    {
        _secrets = secrets;
        _deepSeek = deepSeek;
        _volcanoArk = volcanoArk;
        _tracker = tracker;
    }

    public DeepSeekApiCallTracker Tracker => _tracker;

    public async Task<SolutionResult> SolveAsync(string questionText, CancellationToken ct)
    {
        var provider = await ResolveAsync(ct);
        var result = await provider.SolveAsync(questionText, ct);
        await RecordIfDeepSeekAsync(ct);
        return result;
    }

    public async Task<string> FollowUpAsync(
        string questionText,
        SolutionResult solution,
        string message,
        CancellationToken ct)
    {
        var provider = await ResolveAsync(ct);
        var result = await provider.FollowUpAsync(questionText, solution, message, ct);
        await RecordIfDeepSeekAsync(ct);
        return result;
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken ct)
    {
        var provider = await ResolveAsync(ct);
        var result = await provider.TestAsync(ct);
        if (result.Success)
            await RecordIfDeepSeekAsync(ct);
        return result;
    }

    public async Task<BalanceInfo?> GetBalanceAsync(CancellationToken ct)
    {
        var provider = await ResolveAsync(ct);
        return await provider.GetBalanceAsync(ct);
    }

    private async Task<ITextAnswerProvider> ResolveAsync(CancellationToken ct)
    {
        var stored = await _secrets.GetAsync("ai.provider", ct);
        return AiProviderKindExtensions.FromStorageValue(stored) switch
        {
            AiProviderKind.VolcanoArk => _volcanoArk,
            _ => _deepSeek
        };
    }

    private async Task RecordIfDeepSeekAsync(CancellationToken ct)
    {
        var stored = await _secrets.GetAsync("ai.provider", ct);
        if (AiProviderKindExtensions.FromStorageValue(stored) == AiProviderKind.DeepSeek)
            _tracker.RecordSuccessfulCall();
    }
}
