using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ai;
using CodeTutor.Domain.Solutions;
using CodeTutor.Infrastructure.Ai;

namespace CodeTutor.Infrastructure.Ai;

public sealed class DeepSeekTextAnswerProvider : ITextAnswerProvider
{
    private const string SystemPrompt = """
        你是面向儿童编程学习的题目辅导器。
        只依据用户提供的完整题目作答，不得虚构缺失条件。
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
        如果题干明显缺页、OCR 断裂或条件冲突，needsMoreContext=true，并说明缺少什么。
        不要输出 JSON 以外的内容。
        """;

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;
    private readonly DeepSeekOptions _options;
    private readonly SolutionJsonParser _parser = new();

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DeepSeekTextAnswerProvider(HttpClient http, ISecretStore secrets, DeepSeekOptions options)
    {
        _http = http;
        _secrets = secrets;
        _options = options;
        _http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    public async Task<SolutionResult> SolveAsync(string questionText, CancellationToken ct)
    {
        var (baseUrl, apiKey, model) = await GetConfigAsync(ct);
        EnsureApiKey(apiKey);

        var userMessage = $"""
            以下文本来自多张截图 OCR，可能有少量识别错误。请根据题干作答：

            <question>
            {questionText}
            </question>
            """;

        var content = await SendChatAsync(
            baseUrl,
            apiKey,
            model,
            [
                new ChatMessageDto("system", SystemPrompt),
                new ChatMessageDto("user", userMessage)
            ],
            jsonMode: true,
            maxTokens: 2048,
            ct);

        return await ParseWithRetryAsync(baseUrl, apiKey, model, content, ct);
    }

    public async Task<string> FollowUpAsync(
        string questionText,
        SolutionResult solution,
        string message,
        CancellationToken ct)
    {
        var (baseUrl, apiKey, model) = await GetConfigAsync(ct);
        EnsureApiKey(apiKey);

        var context = $"""
            题目：
            {questionText}

            已有解答：
            类型={solution.QuestionType}
            答案={solution.FinalAnswer}
            思路={solution.Explanation}
            代码={solution.Code}
            """;

        return await SendChatAsync(
            baseUrl,
            apiKey,
            model,
            [
                new ChatMessageDto("system", "你是儿童编程辅导助手。根据已有题目和解答，简洁回答孩子的追问。不要重复整题。"),
                new ChatMessageDto("user", context),
                new ChatMessageDto("user", message)
            ],
            jsonMode: false,
            maxTokens: 512,
            ct);
    }

    public async Task<ProviderTestResult> TestAsync(CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var (baseUrl, apiKey, model) = await GetConfigAsync(ct);
            EnsureApiKey(apiKey);

            _ = await SendChatAsync(
                baseUrl,
                apiKey,
                model,
                [new ChatMessageDto("user", "请回复：连接成功")],
                jsonMode: false,
                maxTokens: 16,
                ct);

            return new ProviderTestResult(true, "DeepSeek 连接成功", DateTimeOffset.UtcNow - started);
        }
        catch (Exception ex)
        {
            return new ProviderTestResult(false, SecretRedactor.Redact(ex.Message), DateTimeOffset.UtcNow - started);
        }
    }

    public async Task<BalanceInfo?> GetBalanceAsync(CancellationToken ct)
    {
        var (baseUrl, apiKey, _) = await GetConfigAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var url = $"{baseUrl.TrimEnd('/')}/user/balance";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var dto = await response.Content.ReadFromJsonAsync<BalanceResponseDto>(ResponseJsonOptions, ct);
        return dto is null ? null : BalanceResponseParser.Parse(dto);
    }

    private async Task<SolutionResult> ParseWithRetryAsync(
        string baseUrl,
        string apiKey,
        string model,
        string content,
        CancellationToken ct)
    {
        var first = _parser.Parse(content, model);
        if (first.Success && first.Solution is not null)
            return first.Solution;

        var fixContent = await SendChatAsync(
            baseUrl,
            apiKey,
            model,
            [
                new ChatMessageDto("system", "将以下内容修复为合法 JSON，不要重新解题，只输出 JSON。"),
                new ChatMessageDto("user", content)
            ],
            jsonMode: true,
            maxTokens: 2048,
            ct);

        var second = _parser.Parse(fixContent, model);
        if (second.Success && second.Solution is not null)
            return second.Solution;

        throw new InvalidOperationException(
            $"无法解析 AI 返回的 JSON：{first.ErrorMessage ?? second.ErrorMessage}");
    }

    private async Task<string> SendChatAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<ChatMessageDto> messages,
        bool jsonMode,
        int maxTokens,
        CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
        var json = DeepSeekChatRequestFactory.Serialize(
            model,
            messages.Select(m => (m.Role, m.Content)).ToList(),
            jsonMode,
            maxTokens,
            _options);

        var attempt = 0;
        while (true)
        {
            attempt++;
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<ChatResponseDto>(ResponseJsonOptions, ct)
                          ?? throw new InvalidOperationException("DeepSeek 返回空响应。");
                var text = dto.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("DeepSeek 未返回文本内容。");
                return text;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var message = MapHttpError(response.StatusCode, errorBody);

            if (attempt <= _options.MaxRetries
                && (response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500))
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                continue;
            }

            throw new InvalidOperationException(message);
        }
    }

    private static string MapHttpError(HttpStatusCode status, string body)
    {
        var redacted = SecretRedactor.Redact(body);
        return status switch
        {
            HttpStatusCode.Unauthorized => "API 密钥无效（401），请检查密钥是否正确。",
            HttpStatusCode.TooManyRequests => "请求过于频繁（429），请稍后重试。",
            HttpStatusCode.BadRequest => $"请求参数错误（400）：{Truncate(redacted, 120)}",
            _ => $"DeepSeek 请求失败（{(int)status}）：{Truncate(redacted, 120)}"
        };
    }

    private async Task<(string BaseUrl, string ApiKey, string Model)> GetConfigAsync(CancellationToken ct)
    {
        var apiKey = await _secrets.GetAsync("deepseek.api_key", ct) ?? string.Empty;
        var model = await _secrets.GetAsync("deepseek.model", ct) ?? _options.Model;
        return (_options.BaseUrl, apiKey, model);
    }

    private static void EnsureApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请先填写 API 密钥并点击「保存并测试」。");
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private sealed record ChatMessageDto(string Role, string Content);

    private sealed class ChatResponseDto
    {
        public List<ChatChoiceDto>? Choices { get; set; }
    }

    private sealed class ChatChoiceDto
    {
        public ChatMessageContentDto? Message { get; set; }
    }

    private sealed class ChatMessageContentDto
    {
        public string? Content { get; set; }
        public string? ReasoningContent { get; set; }
    }

    internal sealed class BalanceResponseDto
    {
        public bool IsAvailable { get; set; }
        public List<BalanceInfoDto>? BalanceInfos { get; set; }
    }

    internal sealed class BalanceInfoDto
    {
        public string? Currency { get; set; }
        public string? TotalBalance { get; set; }
        public string? GrantedBalance { get; set; }
        public string? ToppedUpBalance { get; set; }
    }
}
