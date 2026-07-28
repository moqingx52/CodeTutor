using System.Text.Json;
using System.Text.Json.Serialization;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Application.Ai;

public sealed class SolutionJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public SolutionParseResult Parse(string rawText, string model, string provider = "deepseek")
    {
        try
        {
            var json = ExtractFirstJsonObject(rawText);
            var dto = JsonSerializer.Deserialize<SolutionDto>(json, Options)
                      ?? throw new JsonException("解析结果为空。");
            return new SolutionParseResult(true, Map(dto, model, provider), null);
        }
        catch (Exception ex)
        {
            return new SolutionParseResult(false, null, ex.Message);
        }
    }

    public static string ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            throw new JsonException("响应中未找到 JSON 对象。");

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        throw new JsonException("JSON 对象不完整。");
    }

    private static SolutionResult Map(SolutionDto dto, string model, string provider)
    {
        var questionType = dto.QuestionType?.ToLowerInvariant() switch
        {
            "choice" => QuestionType.Choice,
            "fill" => QuestionType.Fill,
            "programming" => QuestionType.Programming,
            _ => QuestionType.Unknown
        };

        return new SolutionResult(
            questionType,
            dto.FinalAnswer ?? string.Empty,
            dto.Explanation ?? string.Empty,
            dto.Code ?? string.Empty,
            dto.ProgrammingLanguage ?? "unknown",
            dto.NeedsMoreContext,
            dto.Confidence,
            provider,
            model,
            DateTimeOffset.UtcNow);
    }

    private sealed class SolutionDto
    {
        public string? QuestionType { get; set; }
        public string? FinalAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? Code { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public bool NeedsMoreContext { get; set; }
        public double Confidence { get; set; }
    }
}

public sealed record SolutionParseResult(bool Success, SolutionResult? Solution, string? ErrorMessage);
