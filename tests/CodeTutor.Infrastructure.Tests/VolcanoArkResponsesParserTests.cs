using CodeTutor.Infrastructure.Ai;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public sealed class VolcanoArkResponsesParserTests
{
    [Fact]
    public void ExtractOutputText_reads_output_text_from_responses_payload()
    {
        const string json = """
            {
              "status": "completed",
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [
                    {
                      "type": "output_text",
                      "text": "{\"questionType\":\"choice\",\"finalAnswer\":\"A\"}"
                    }
                  ]
                }
              ]
            }
            """;

        var text = VolcanoArkResponsesParser.ExtractOutputText(json);

        text.Should().Contain("\"questionType\":\"choice\"");
    }

    [Fact]
    public void ExtractOutputText_throws_when_no_text_found()
    {
        const string json = """{ "status": "completed", "output": [] }""";

        var act = () => VolcanoArkResponsesParser.ExtractOutputText(json);

        act.Should().Throw<InvalidOperationException>();
    }
}
