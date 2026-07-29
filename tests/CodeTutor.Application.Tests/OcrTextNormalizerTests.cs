using CodeTutor.Application.Ocr;
using FluentAssertions;

namespace CodeTutor.Application.Tests;

public sealed class OcrTextNormalizerTests
{
    [Fact]
    public void Flatten_removes_carriage_return_and_line_feed()
    {
        OcrTextNormalizer.Flatten("第一行\n第二行\r\n第三行")
            .Should().Be("第一行第二行第三行");
    }

    [Fact]
    public void Flatten_returns_empty_for_null_or_whitespace_only_newlines()
    {
        OcrTextNormalizer.Flatten("\n\r\n").Should().BeEmpty();
    }
}
