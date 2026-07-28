namespace CodeTutor.Infrastructure.Ai;

using CodeTutor.Application.Ai;

public sealed class VolcanoArkOptions
{
    public string BaseUrl { get; init; } = AiProviderDefaults.VolcanoArkBaseUrl;
    public string Model { get; init; } = AiProviderDefaults.VolcanoArkDefaultModel;
    public int TimeoutSeconds { get; init; } = 120;
    public int MaxImages { get; init; } = 8;
}
