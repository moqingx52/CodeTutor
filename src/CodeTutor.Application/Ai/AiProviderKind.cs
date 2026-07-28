namespace CodeTutor.Application.Ai;

public enum AiProviderKind
{
    DeepSeek = 0,
    VolcanoArk = 1
}

public static class AiProviderKindExtensions
{
    public static string ToStorageValue(this AiProviderKind kind) =>
        kind switch
        {
            AiProviderKind.VolcanoArk => "volcano_ark",
            _ => "deepseek"
        };

    public static AiProviderKind FromStorageValue(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "volcano_ark" or "volcano" or "volcanoark" => AiProviderKind.VolcanoArk,
            _ => AiProviderKind.DeepSeek
        };

    public static string ToDisplayName(this AiProviderKind kind) =>
        kind switch
        {
            AiProviderKind.VolcanoArk => "火山方舟",
            _ => "DeepSeek"
        };

    public static string GetDefaultBaseUrl(this AiProviderKind kind) =>
        kind switch
        {
            AiProviderKind.VolcanoArk => AiProviderDefaults.VolcanoArkBaseUrl,
            _ => AiProviderDefaults.DeepSeekBaseUrl
        };
}

public static class AiProviderDefaults
{
    public const string DeepSeekBaseUrl = "https://api.deepseek.com";
    public const string VolcanoArkBaseUrl = "https://ark.cn-beijing.volces.com/api/plan/v3";
    public const string DeepSeekDefaultModel = "deepseek-v4-pro";
    public const string VolcanoArkDefaultModel = "doubao-seed-1-6-vision-250815";
}
