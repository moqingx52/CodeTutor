using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Application.State;

namespace CodeTutor.Application.UseCases;

public sealed class SolveTextUseCase : ISolveTextUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ITextAnswerProvider _provider;

    public SolveTextUseCase(IAppSessionContext session, ITextAnswerProvider provider)
    {
        _session = session;
        _provider = provider;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var session = _session.Current;
        if (string.IsNullOrWhiteSpace(session.WorkingQuestionText))
            throw new InvalidOperationException("累计题干为空，无法解答。");

        var solution = await _provider.SolveAsync(session.WorkingQuestionText, ct);
        var updated = session with
        {
            Solution = solution,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _session.ApplySession(updated);
        await _session.PersistCurrentAsync(ct);
    }
}

public sealed class SolveVisionUseCase : ISolveVisionUseCase
{
    private readonly IAppSessionContext _session;
    private readonly IVisionAnswerProvider _provider;

    public SolveVisionUseCase(IAppSessionContext session, IVisionAnswerProvider provider)
    {
        _session = session;
        _provider = provider;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_provider.IsConfigured)
            throw new InvalidOperationException("视觉模型未配置。");

        var session = _session.Current;
        if (session.Captures.Count == 0)
            throw new InvalidOperationException("当前会话没有截图。");

        var paths = session.Captures.Select(c => c.ImagePath).ToList();
        var solution = await _provider.SolveFromImagesAsync(paths, ct);

        var updated = session with
        {
            Solution = solution,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _session.ApplySession(updated);
        await _session.PersistCurrentAsync(ct);
    }
}

public sealed class SaveAndTestApiUseCase : ISaveAndTestApiUseCase
{
    private readonly ISecretStore _secrets;
    private readonly ITextAnswerProvider _textProvider;
    private readonly IVisionAnswerProvider _visionProvider;

    public SaveAndTestApiUseCase(
        ISecretStore secrets,
        ITextAnswerProvider textProvider,
        IVisionAnswerProvider visionProvider)
    {
        _secrets = secrets;
        _textProvider = textProvider;
        _visionProvider = visionProvider;
    }

    public async Task ExecuteAsync(AiProviderKind provider, string apiKey, string model, CancellationToken ct)
    {
        await _secrets.SaveAsync("ai.provider", provider.ToStorageValue(), ct);

        if (provider == AiProviderKind.VolcanoArk)
        {
            await _secrets.SaveAsync("volcano.api_key", apiKey, ct);
            await _secrets.SaveAsync("volcano.model", model, ct);

            var result = await _visionProvider.TestAsync(ct);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);
            return;
        }

        await _secrets.SaveAsync("deepseek.api_key", apiKey, ct);
        await _secrets.SaveAsync("deepseek.model", model, ct);

        var textResult = await _textProvider.TestAsync(ct);
        if (!textResult.Success)
            throw new InvalidOperationException(textResult.Message);
    }
}

public sealed class SendFollowUpUseCase : ISendFollowUpUseCase
{
    private readonly IAppSessionContext _session;
    private readonly ITextAnswerProvider _provider;

    public SendFollowUpUseCase(IAppSessionContext session, ITextAnswerProvider provider)
    {
        _session = session;
        _provider = provider;
    }

    public async Task ExecuteAsync(string message, CancellationToken ct)
    {
        var session = _session.Current;
        if (session.Solution is null)
            throw new InvalidOperationException("请先完成文字解答。");

        var reply = await _provider.FollowUpAsync(
            session.WorkingQuestionText,
            session.Solution,
            message,
            ct);

        var userMsg = new Domain.Common.ChatMessage(
            Guid.NewGuid(),
            Domain.Common.FeedbackMessageType.User,
            message,
            DateTimeOffset.UtcNow);

        var assistantMsg = new Domain.Common.ChatMessage(
            Guid.NewGuid(),
            Domain.Common.FeedbackMessageType.Assistant,
            reply,
            DateTimeOffset.UtcNow);

        var updated = session with
        {
            ChatMessages = session.ChatMessages.Append(userMsg).Append(assistantMsg).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _session.ApplySession(updated);
        await _session.PersistCurrentAsync(ct);
    }
}
