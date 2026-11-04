using System.Text.Json;
using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Reads the lines of <c>claude -p --output-format stream-json --include-partial-messages</c>, one JSON event per line.
/// The skill file is the input of Claude Code's <c>StructuredOutput</c> tool, so its text arrives as
/// <c>input_json_delta</c> pieces; those are decoded and reported for display. Anything else Claude says is not shown.
/// The last line, <c>type: "result"</c>, is kept untouched: it holds the one verified answer.
/// </summary>
internal sealed class ClaudeCodeStream(IProgress<AdaptationProgress> progress)
{
    private const string StructuredOutputTool = "StructuredOutput";

    private SkillFileJsonDecoder? _decoder;
    private int _decoderIndex;

    /// <summary>The result line, or null while it has not arrived (a stream that ended early has none).</summary>
    public string? ResultLine { get; private set; }

    public void Read(string line)
    {
        if (!TryParse(line, out var root))
        {
            return;
        }

        using (root)
        {
            switch (TextOf(root.RootElement, "type"))
            {
                case "result":
                    ResultLine = line;
                    break;
                case "stream_event" when root.RootElement.TryGetProperty("event", out var streamEvent) && streamEvent.ValueKind == JsonValueKind.Object:
                    ReadEvent(streamEvent);
                    break;
            }
        }
    }

    private void ReadEvent(JsonElement streamEvent)
    {
        if (!streamEvent.TryGetProperty("index", out var index) || !index.TryGetInt32(out var blockIndex))
        {
            return;
        }

        switch (TextOf(streamEvent, "type"))
        {
            case "content_block_start" when IsStructuredOutput(streamEvent):
                BeginSkillFile(blockIndex);
                break;
            case "content_block_delta" when _decoder is not null && blockIndex == _decoderIndex:
                ReadPiece(streamEvent);
                break;
        }
    }

    private void BeginSkillFile(int blockIndex)
    {
        if (_decoder is not null)
        {
            progress.Report(new AdaptationProgress.StartedOver());
        }

        _decoder = new SkillFileJsonDecoder();
        _decoderIndex = blockIndex;
    }

    private void ReadPiece(JsonElement streamEvent)
    {
        if (!streamEvent.TryGetProperty("delta", out var delta)
            || delta.ValueKind != JsonValueKind.Object
            || TextOf(delta, "type") != "input_json_delta"
            || TextOf(delta, "partial_json") is not { } partialJson)
        {
            return;
        }

        var text = _decoder!.Append(partialJson);
        if (text.Length > 0)
        {
            progress.Report(new AdaptationProgress.TextArrived(text));
        }
    }

    private static bool IsStructuredOutput(JsonElement streamEvent) =>
        streamEvent.TryGetProperty("content_block", out var block)
        && block.ValueKind == JsonValueKind.Object
        && TextOf(block, "type") == "tool_use"
        && TextOf(block, "name") == StructuredOutputTool;

    private static string? TextOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryParse(string line, out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(line);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
