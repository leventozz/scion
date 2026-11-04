using System.Text.Json;

namespace Scion.Infrastructure.Codex;

/// <summary>The last message of the agent, read for the one thing Scion wants: the skill file.</summary>
internal abstract record CodexAnswer
{
    private CodexAnswer()
    {
    }

    public sealed record SkillFile(string Text) : CodexAnswer;

    /// <summary>There was no message at all.</summary>
    public sealed record Missing : CodexAnswer;

    /// <summary>There was a message, but it is not the JSON with a skill file in it.</summary>
    public sealed record Unreadable : CodexAnswer;

    public static CodexAnswer Read(string? agentMessage)
    {
        if (string.IsNullOrWhiteSpace(agentMessage))
        {
            return new Missing();
        }

        try
        {
            using var document = JsonDocument.Parse(agentMessage);
            var text = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("skillFile", out var skillFile)
                && skillFile.ValueKind == JsonValueKind.String
                    ? skillFile.GetString()
                    : null;

            return string.IsNullOrWhiteSpace(text) ? new Unreadable() : new SkillFile(text);
        }
        catch (JsonException)
        {
            return new Unreadable();
        }
    }
}
