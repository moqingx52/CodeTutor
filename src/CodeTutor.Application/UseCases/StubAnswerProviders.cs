using CodeTutor.Application.Ai;
using CodeTutor.Application.Abstractions;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Application.UseCases;

public sealed class NotConfiguredTextAnswerProvider : ITextAnswerProvider
{
    public Task<SolutionResult> SolveAsync(string questionText, CancellationToken ct) =>
        throw new InvalidOperationException("DeepSeek 接口尚未配置，请先填写 API 密钥并点击「保存并测试」。");

    public Task<string> FollowUpAsync(
        string questionText,
        SolutionResult solution,
        string message,
        CancellationToken ct) =>
        throw new InvalidOperationException("DeepSeek 接口尚未配置。");

    public Task<ProviderTestResult> TestAsync(CancellationToken ct) =>
        Task.FromResult(new ProviderTestResult(false, "DeepSeek 接口尚未配置。", TimeSpan.Zero));

    public Task<BalanceInfo?> GetBalanceAsync(CancellationToken ct) =>
        Task.FromResult<BalanceInfo?>(null);
}

public sealed class NotConfiguredVisionAnswerProvider : IVisionAnswerProvider
{
    public bool IsConfigured => false;

    public Task<SolutionResult> SolveFromImagesAsync(IReadOnlyList<string> imagePaths, CancellationToken ct) =>
        throw new InvalidOperationException("视觉模型尚未配置，请填写火山方舟 API 密钥并点击「保存并测试」。");

    public Task<ProviderTestResult> TestAsync(CancellationToken ct) =>
        Task.FromResult(new ProviderTestResult(false, "视觉模型尚未配置。", TimeSpan.Zero));
}
