using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeTutor.Infrastructure.Ai;

internal static class DeepSeekChatRequestFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(
        string model,
        IReadOnlyList<(string Role, string Content)> messages,
        bool jsonMode,
        int maxTokens,
        DeepSeekOptions options)
    {
        var body = new ChatRequestDto
        {
            Model = model,
            Messages = messages.Select(m => new ChatMessageDto(m.Role, m.Content)).ToList(),
            MaxTokens = maxTokens,
            ResponseFormat = jsonMode ? new ResponseFormatDto("json_object") : null,
            Thinking = new ThinkingDto(options.ThinkingEnabled ? "enabled" : "disabled"),
            ReasoningEffort = options.ThinkingEnabled ? "high" : null
        };

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private sealed record ChatMessageDto(string Role, string Content);

    private sealed class ChatRequestDto
    {
        public string Model { get; set; } = string.Empty;
        public List<ChatMessageDto> Messages { get; set; } = [];
        public int MaxTokens { get; set; }
        public ResponseFormatDto? ResponseFormat { get; set; }
        public ThinkingDto Thinking { get; set; } = new("disabled");
        public string? ReasoningEffort { get; set; }
    }

    private sealed record ThinkingDto(string Type);

    private sealed record ResponseFormatDto(string Type);
}
