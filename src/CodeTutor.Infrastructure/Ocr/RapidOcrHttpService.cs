using System.Net.Http.Json;
using System.Text.Json;
using CodeTutor.Application.Abstractions;
using CodeTutor.Application.Ocr;
using CodeTutor.Domain.Ocr;

namespace CodeTutor.Infrastructure.Ocr;

public sealed class RapidOcrHttpService : IOcrService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RapidOcrHttpService(HttpClient http) => _http = http;

    public async Task<bool> HealthCheckAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("/healthz", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<OcrResult> RecognizeAsync(Stream image, OcrRequestOptions options, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(image), "image", "capture.png");
        content.Add(new StringContent(options.Profile), "profile");
        content.Add(new StringContent(options.Language), "language");
        if (!string.IsNullOrEmpty(options.RequestId))
            content.Add(new StringContent(options.RequestId), "request_id");

        var started = DateTimeOffset.UtcNow;
        var response = await _http.PostAsync("/v1/ocr", content, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<OcrResponseDto>(JsonOptions, ct)
                  ?? throw new InvalidOperationException("Empty OCR response.");

        return new OcrResult(
            OcrTextNormalizer.Flatten(dto.FullText ?? string.Empty),
            dto.MeanConfidence,
            DateTimeOffset.UtcNow - started,
            dto.Lines?.Select(l => new OcrLine(
                l.Text ?? string.Empty,
                l.Confidence,
                l.Polygon?.Select(p => new OcrPoint(p[0], p[1])).ToList() ?? []))
                .ToList() ?? []);
    }

    private sealed class OcrResponseDto
    {
        public string? FullText { get; set; }
        public double MeanConfidence { get; set; }
        public List<OcrLineDto>? Lines { get; set; }
    }

    private sealed class OcrLineDto
    {
        public string? Text { get; set; }
        public double Confidence { get; set; }
        public List<float[]>? Polygon { get; set; }
    }
}
