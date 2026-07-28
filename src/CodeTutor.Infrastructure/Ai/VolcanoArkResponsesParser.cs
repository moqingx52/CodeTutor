using System.Text.Json;

namespace CodeTutor.Infrastructure.Ai;

internal static class VolcanoArkResponsesParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ExtractOutputText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type)
                        && type.GetString() == "output_text"
                        && part.TryGetProperty("text", out var text))
                    {
                        var value = text.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
        }

        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            var first = choices.EnumerateArray().FirstOrDefault();
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var messageContent))
            {
                var value = messageContent.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        throw new InvalidOperationException("火山方舟响应中未找到文本输出。");
    }
}
