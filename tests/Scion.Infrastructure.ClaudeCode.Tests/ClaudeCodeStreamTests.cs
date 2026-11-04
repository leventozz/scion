using System.Text.Json;
using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode.Tests;

public class ClaudeCodeStreamTests
{
    private readonly List<AdaptationProgress> _reports = [];
    private readonly ClaudeCodeStream _stream;

    public ClaudeCodeStreamTests() => _stream = new ClaudeCodeStream(new Recording(_reports));

    private static string Event(object streamEvent) => JsonSerializer.Serialize(new { type = "stream_event", @event = streamEvent });

    private static string StartOf(int index, string toolName = "StructuredOutput") =>
        Event(new { type = "content_block_start", index, content_block = new { type = "tool_use", id = "toolu_1", name = toolName, input = new { } } });

    private static string JsonPiece(int index, string partialJson) =>
        Event(new { type = "content_block_delta", index, delta = new { type = "input_json_delta", partial_json = partialJson } });

    private static string TextPiece(int index, string text) =>
        Event(new { type = "content_block_delta", index, delta = new { type = "text_delta", text } });

    private void Feed(params string[] lines)
    {
        foreach (var line in lines)
        {
            _stream.Read(line);
        }
    }

    private IEnumerable<string> Texts => _reports.OfType<AdaptationProgress.TextArrived>().Select(report => report.Text);

    [Fact]
    public void Reports_the_skill_file_text_as_the_pieces_of_the_structured_output_arrive()
    {
        Feed(StartOf(1), JsonPiece(1, "{\"skillFile\":\"---\\nna"), JsonPiece(1, "me: docker"), JsonPiece(1, "\"}"));

        Assert.Equal(["---\nna", "me: docker"], Texts);
    }

    [Fact]
    public void Reports_nothing_for_a_piece_that_adds_no_text()
    {
        Feed(StartOf(0), JsonPiece(0, ""), JsonPiece(0, "{\"skill"), JsonPiece(0, "File\":\""));

        Assert.Empty(_reports);
    }

    [Fact]
    public void Ignores_text_that_is_not_the_structured_output()
    {
        Feed(
            Event(new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } }),
            TextPiece(0, "Here is your skill."),
            StartOf(1, "Read"),
            JsonPiece(1, "{\"skillFile\":\"not this\"}"));

        Assert.Empty(_reports);
    }

    [Fact]
    public void Ignores_pieces_of_other_blocks_while_the_structured_output_is_written()
    {
        Feed(StartOf(1), JsonPiece(0, "{\"skillFile\":\"other block\"}"), JsonPiece(1, "{\"skillFile\":\"mine\"}"));

        Assert.Equal(["mine"], Texts);
    }

    [Fact]
    public void A_second_structured_output_starts_over_with_its_own_text()
    {
        Feed(
            StartOf(0),
            JsonPiece(0, "{\"skillFile\":\"first try\"}"),
            StartOf(1),
            JsonPiece(1, "{\"skillFile\":\"second try\"}"));

        Assert.Equal(
            [
                new AdaptationProgress.TextArrived("first try"),
                new AdaptationProgress.StartedOver(),
                new AdaptationProgress.TextArrived("second try"),
            ],
            _reports);
    }

    [Fact]
    public void Reads_the_shape_the_real_claude_code_writes_a_thinking_block_first_and_a_space_after_the_colon()
    {
        Feed(
            Event(new { type = "content_block_start", index = 0, content_block = new { type = "thinking", thinking = "" } }),
            Event(new { type = "content_block_delta", index = 0, delta = new { type = "thinking_delta", thinking = "Let me write it." } }),
            StartOf(1),
            JsonPiece(1, "{\"skillFile\": \"---\\nname: demo"),
            JsonPiece(1, "\\n---\\n# Demo\\n\"}"));

        Assert.Equal(["---\nname: demo", "\n---\n# Demo\n"], Texts);
    }

    [Fact]
    public void Keeps_the_result_line_untouched()
    {
        const string Result = "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"structured_output\":{\"skillFile\":\"x\"}}";

        Feed("{\"type\":\"system\",\"subtype\":\"init\"}", Result);

        Assert.Equal(Result, _stream.ResultLine);
    }

    [Fact]
    public void Has_no_result_line_until_the_result_has_arrived()
    {
        Feed(StartOf(0), JsonPiece(0, "{\"skillFile\":\"x"));

        Assert.Null(_stream.ResultLine);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not json")]
    [InlineData("[1, 2]")]
    [InlineData("{\"type\":\"stream_event\"}")]
    [InlineData("{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"index\":0}}")]
    [InlineData("{\"type\":\"stream_event\",\"event\":\"nope\"}")]
    public void Ignores_a_line_that_is_not_an_event_it_knows(string line)
    {
        Feed(StartOf(0), line, JsonPiece(0, "{\"skillFile\":\"still works\"}"));

        Assert.Equal(["still works"], Texts);
        Assert.Null(_stream.ResultLine);
    }

    private sealed class Recording(List<AdaptationProgress> reports) : IProgress<AdaptationProgress>
    {
        public void Report(AdaptationProgress value) => reports.Add(value);
    }
}
