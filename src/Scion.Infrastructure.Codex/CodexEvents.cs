using System.Text.Json;

namespace Scion.Infrastructure.Codex;

/// <summary>
/// Reads the lines <c>codex exec --json</c> prints, one at a time, for what Scion needs: the last thing the agent said, why
/// a turn failed, and whether a tool ran. Scion starts Codex with every tool switched off, so a tool that ran anyway is
/// something it did not ask for: the run is stopped (<paramref name="onToolRan"/>) and its answer thrown away.
/// <para>
/// A finished item counts as a tool unless it is plain text, reasoning or an error (every run reports "code mode is
/// unavailable" as an error item: that is the tool being refused, not run), or the tool itself failed or was declined. A
/// kind Scion has never seen counts as a tool: when unsure, the run fails closed.
/// </para>
/// </summary>
internal sealed class CodexEvents(Action onToolRan)
{
    private static readonly string[] NotTools = ["agent_message", "reasoning", "error"];
    private static readonly string[] DidNotRun = ["failed", "declined"];

    /// <summary>The text of the last agent message; the answer is in the last one.</summary>
    public string? LastAgentMessage { get; private set; }

    /// <summary>The message of a failed turn.</summary>
    public string? TurnFailure { get; private set; }

    public bool ToolRan { get; private set; }

    /// <summary>Takes one line of output. A line that is not an event is ignored.</summary>
    public void Read(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                Take(document.RootElement);
            }
        }
        catch (JsonException)
        {
        }
    }

    private void Take(JsonElement envelope)
    {
        switch (StringOf(envelope, "type"))
        {
            case "item.completed" when envelope.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object:
                TakeItem(item);
                break;
            case "turn.failed" when envelope.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object:
                TurnFailure = StringOf(error, "message");
                break;
        }
    }

    private void TakeItem(JsonElement item)
    {
        var type = StringOf(item, "type");
        if (type == "agent_message")
        {
            LastAgentMessage = StringOf(item, "text") ?? LastAgentMessage;
        }

        if (type is null || NotTools.Contains(type) || DidNotRun.Contains(StringOf(item, "status")))
        {
            return;
        }

        if (!ToolRan)
        {
            ToolRan = true;
            onToolRan();
        }
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
