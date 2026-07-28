using CodeTutor.Infrastructure.Ai;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public sealed class DeepSeekRequestBuilderTests
{
    [Fact]
    public void Serialize_includes_thinking_disabled_by_default()
    {
        var options = new DeepSeekOptions { ThinkingEnabled = false };

        var json = DeepSeekChatRequestFactory.Serialize(
            "deepseek-v4-pro",
            [("user", "hello")],
            jsonMode: false,
            maxTokens: 16,
            options);

        json.Should().Contain("\"thinking\"");
        json.Should().Contain("\"type\":\"disabled\"");
        json.Should().NotContain("\"reasoning_effort\"");
    }

    [Fact]
    public void Serialize_includes_thinking_enabled_when_configured()
    {
        var options = new DeepSeekOptions { ThinkingEnabled = true };

        var json = DeepSeekChatRequestFactory.Serialize(
            "deepseek-v4-pro",
            [("user", "hello")],
            jsonMode: true,
            maxTokens: 2048,
            options);

        json.Should().Contain("\"type\":\"enabled\"");
        json.Should().Contain("\"reasoning_effort\":\"high\"");
    }
}
