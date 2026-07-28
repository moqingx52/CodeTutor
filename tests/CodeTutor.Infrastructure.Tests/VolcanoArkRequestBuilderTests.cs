using System.Text.Json;
using CodeTutor.Infrastructure.Ai;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public sealed class VolcanoArkRequestBuilderTests
{
    [Fact]
    public void Serialize_request_uses_input_image_and_input_text()
    {
        var body = new VolcanoArkVisionAnswerProvider.ResponsesRequestDto
        {
            Model = "doubao-seed-1-6-vision-250815",
            Input =
            [
                new VolcanoArkVisionAnswerProvider.InputMessageDto(
                    "user",
                    [
                        new VolcanoArkVisionAnswerProvider.InputContentDto(
                            "input_image",
                            ImageUrl: "data:image/png;base64,abc"),
                        new VolcanoArkVisionAnswerProvider.InputContentDto(
                            "input_text",
                            Text: "请解答题目")
                    ])
            ]
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        json.Should().Contain("\"model\":\"doubao-seed-1-6-vision-250815\"");
        json.Should().Contain("\"type\":\"input_image\"");
        json.Should().Contain("\"image_url\":\"data:image/png;base64,abc\"");
        json.Should().Contain("\"type\":\"input_text\"");
        json.Should().Contain("\\u8BF7\\u89E3\\u7B54\\u9898\\u76EE");
    }
}
