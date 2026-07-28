using CodeTutor.Application.Ai;
using CodeTutor.Domain.Solutions;
using FluentAssertions;

namespace CodeTutor.Application.Tests;

public class SolutionJsonParserTests
{
    private readonly SolutionJsonParser _parser = new();

    [Fact]
    public void Parse_ValidChoiceJson_ReturnsSolution()
    {
        const string json = """
            {
              "questionType": "choice",
              "finalAnswer": "B",
              "explanation": "因为……",
              "code": "",
              "programmingLanguage": "unknown",
              "needsMoreContext": false,
              "confidence": 0.95
            }
            """;

        var result = _parser.Parse(json, "deepseek-chat");
        result.Success.Should().BeTrue();
        result.Solution!.QuestionType.Should().Be(QuestionType.Choice);
        result.Solution.FinalAnswer.Should().Be("B");
    }

    [Fact]
    public void Parse_ProgrammingJson_ReturnsCode()
    {
        const string json = """
            {
              "questionType": "programming",
              "finalAnswer": "",
              "explanation": "",
              "code": "print(1)",
              "programmingLanguage": "python",
              "needsMoreContext": false,
              "confidence": 0.9
            }
            """;

        var result = _parser.Parse(json, "deepseek-chat");
        result.Solution!.QuestionType.Should().Be(QuestionType.Programming);
        result.Solution.Code.Should().Be("print(1)");
    }

    [Fact]
    public void ExtractFirstJsonObject_FromWrappedText_Works()
    {
        const string text = """
            这是模型多余说明
            {"questionType":"fill","finalAnswer":"42","explanation":"x","code":"","programmingLanguage":"unknown","needsMoreContext":false,"confidence":0.8}
            """;

        var json = SolutionJsonParser.ExtractFirstJsonObject(text);
        var result = _parser.Parse(json, "deepseek-chat");
        result.Success.Should().BeTrue();
        result.Solution!.FinalAnswer.Should().Be("42");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFailure()
    {
        var result = _parser.Parse("not json", "deepseek-chat");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
