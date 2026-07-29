using CodeTutor.Application.State;
using CodeTutor.Domain.Ocr;
using FluentAssertions;

namespace CodeTutor.Domain.Tests;

public class QuestionTextMergerTests
{
    private readonly QuestionTextMerger _merger = new();

    [Fact]
    public void Merge_FirstCapture_ReturnsIncomingText()
    {
        var incoming = new OcrResult("第一行第二行", 0.95, TimeSpan.Zero, []);
        var result = _merger.Merge(string.Empty, incoming);
        result.MergedText.Should().Be("第一行第二行");
        result.Decision.Strategy.Should().Be(MergeStrategy.First);
    }

    [Fact]
    public void Merge_LineOverlap_RemovesDuplicateLines()
    {
        var existing = "题目：编写函数计算列表中偶数之和。输入第一行是整数 n。第二行包含 n 个整数。";
        var incoming = new OcrResult(
            "第二行包含 n 个整数。输出所有偶数的和。示例输入：5",
            0.94, TimeSpan.Zero, []);

        var result = _merger.Merge(existing, incoming);
        result.MergedText.Should().Contain("输出所有偶数的和");
        result.MergedText.Should().NotContain("整数。第二行包含 n 个整数。第二行");
    }

    [Fact]
    public void Merge_NoOverlap_appends_text_without_separator_marker()
    {
        var existing = "第一段题目文字";
        var incoming = new OcrResult("第二段完全不同", 0.9, TimeSpan.Zero, []);

        var result = _merger.Merge(existing, incoming);

        result.MergedText.Should().Be("第一段题目文字第二段完全不同");
        result.Decision.Strategy.Should().Be(MergeStrategy.NoOverlapWithWarning);
    }
}
