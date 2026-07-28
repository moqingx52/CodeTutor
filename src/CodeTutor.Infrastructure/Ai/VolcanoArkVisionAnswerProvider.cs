using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Domain.Solutions;

namespace CodeTutor.Infrastructure.Ai;

public sealed class VolcanoArkVisionAnswerProvider : IVisionAnswerProvider
{
    private const string SystemPrompt = """
        你是面向儿童编程学习的题目辅导器。
        用户会提供多张题目截图，请综合所有图片内容作答，不得虚构缺失条件。
        先判断题目类型：choice、fill、programming、unknown。

        输出必须是严格 JSON：
        {
          "questionType": "...",
          "finalAnswer": "...",
          "explanation": "...",
          "code": "...",
          "programmingLanguage": "...",
          "needsMoreContext": false,
          "confidence": 0.0
        }

        选择题和填空题：先在 finalAnswer 给出直接答案，再在 explanation 给出简洁思路。
        编程题：将完整可运行代码放在 code；finalAnswer 和 explanation 留空。
        如果图片明显缺页或条件冲突，needsMoreContext=true，并说明缺少什么。
        不要输出 JSON 以外的内容。
        """;

    private const string SolveUserPrompt = """
        以下是多张题目截图，请识别并解答。若 OCR 可能有误，以图片内容为准。
        按系统要求输出严格 JSON。
        """;

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly VolcanoArkOptions _options;
    private readonly SolutionJsonParser _parser = new();

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public VolcanoArkVisionAnswerProvider(HttpClient http, ISecretStore secrets, VolcanoArkOptions options)
    {
        _http = http;
        _secrets = secrets;
        _options = options;
        _http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKeySync());

    public async Task<SolutionResult> SolveFromImagesAsync(IReadOnlyList<string> imagePaths, CancellationToken ct)
    {
        if (imagePaths.Count == 0)
            throw new InvalidOperationException("没有可上传的截图。");

        var (baseUrl, apiKey, model) = await GetConfigAsync(ct);
        EnsureApiKey(apiKey);

        var limitedPaths = imagePaths.Take(_options.MaxImages).ToList();
        var contentParts = BuildImageContentParts(limitedPaths);
        contentParts.Add(new InputContentDto("input_text", Text: $"{SystemPrompt}\n\n{SolveUserPrompt}"));

        var responseJson = await SendResponsesAsync(
            baseUrl,
            apiKey,
            model,
            [new InputMessageDto("user", contentParts)],
            ct);

        var outputText = VolcanoArkResponsesParser.ExtractOutputText(responseJson);
        return await ParseWithRetryAsync(baseUrl, apiKey, model, outputText, ct);
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var (baseUrl, apiKey, model) = await GetConfigAsync(ct);
            EnsureApiKey(apiKey);

            _ = await SendResponsesAsync(
                baseUrl,
                apiKey,
                model,
                [new InputMessageDto("user", [new InputContentDto("input_text", Text: "请回复：连接成功")])],
                ct);

            return new ProviderTestResult(true, "火山方舟连接成功", DateTimeOffset.UtcNow - started);
        }
        catch (Exception ex)
        {
            return new ProviderTestResult(false, SecretRedactor.Redact(ex.Message), DateTimeOffset.UtcNow - started);
        }
    }

    private async Task<SolutionResult> ParseWithRetryAsync(
        string baseUrl,
        string apiKey,
        string model,
        string content,
        CancellationToken ct)
    {
        var first = _parser.Parse(content, model, "volcano_ark");
        if (first.Success && first.Solution is not null)
            return first.Solution;

        var fixJson = await SendResponsesAsync(
            baseUrl,
            apiKey,
            model,
            [
                new InputMessageDto("user", [
                    new InputContentDto("input_text", Text: "将以下内容修复为合法 JSON，不要重新解题，只输出 JSON。"),
                    new InputContentDto("input_text", Text: content)
                ])
            ],
            ct);

        var fixedText = VolcanoArkResponsesParser.ExtractOutputText(fixJson);
        var second = _parser.Parse(fixedText, model, "volcano_ark");
        if (second.Success && second.Solution is not null)
            return second.Solution;

        throw new InvalidOperationException(
            $"无法解析火山方舟返回的 JSON：{first.ErrorMessage ?? second.ErrorMessage}");
    }

    private async Task<string> SendResponsesAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<InputMessageDto> input,
        CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/responses";
        var body = new ResponsesRequestDto { Model = model, Input = input.ToList() };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, RequestJsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(MapHttpError(response.StatusCode, responseBody));

        return responseBody;
    }

    private static List<InputContentDto> BuildImageContentParts(IReadOnlyList<string> imagePaths)
    {
        var parts = new List<InputContentDto>();
        foreach (var path in imagePaths)
        {
            if (!File.Exists(path))
                continue;

            var bytes = File.ReadAllBytes(path);
            var mime = GuessMimeType(path);
            var base64 = Convert.ToBase64String(bytes);
            parts.Add(new InputContentDto(
                "input_image",
                ImageUrl: $"data:{mime};base64,{base64}"));
        }

        if (parts.Count == 0)
            throw new InvalidOperationException("截图文件不存在或无法读取。");

        return parts;
    }

    private static string GuessMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };

    private static string MapHttpError(HttpStatusCode status, string body)
    {
        var redacted = SecretRedactor.Redact(body);
        return status switch
        {
            HttpStatusCode.Unauthorized => "火山方舟 API 密钥无效（401），请检查 Agent Plan API Key。",
            HttpStatusCode.TooManyRequests => "火山方舟请求过于频繁（429），请稍后重试。",
            HttpStatusCode.BadRequest => $"火山方舟请求参数错误（400）：{Truncate(redacted, 120)}",
            _ => $"火山方舟请求失败（{(int)status}）：{Truncate(redacted, 120)}"
        };
    }

    private async Task<(string BaseUrl, string ApiKey, string Model)> GetConfigAsync(CancellationToken ct)
    {
        var apiKey = await _secrets.GetAsync("volcano.api_key", ct) ?? string.Empty;
        var model = await _secrets.GetAsync("volcano.model", ct) ?? _options.Model;
        return (_options.BaseUrl, apiKey, model);
    }

    private string? GetApiKeySync() =>
        _secrets.GetAsync("volcano.api_key", CancellationToken.None).GetAwaiter().GetResult();

    private static void EnsureApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请先填写火山方舟 API 密钥并点击「保存并测试」。");
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    internal sealed class ResponsesRequestDto
    {
        public string Model { get; set; } = string.Empty;
        public List<InputMessageDto> Input { get; set; } = [];
    }

    internal sealed record InputMessageDto(string Role, List<InputContentDto> Content);

    internal sealed record InputContentDto(
        string Type,
        string? Text = null,
        [property: JsonPropertyName("image_url")] string? ImageUrl = null);
}
