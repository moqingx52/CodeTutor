namespace CodeTutor.Infrastructure.Ai;

using CodeTutor.Application.Ai;

public sealed class DeepSeekOptions
{
    public string BaseUrl { get; init; } = AiProviderDefaults.DeepSeekBaseUrl;
    public string Model { get; init; } = AiProviderDefaults.DeepSeekDefaultModel;
    public int TimeoutSeconds { get; init; } = 60;
    public bool ThinkingEnabled { get; init; }
    public int MaxRetries { get; init; } = 2;
}
