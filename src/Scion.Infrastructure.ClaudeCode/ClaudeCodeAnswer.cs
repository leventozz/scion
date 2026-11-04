using System.Text.Json;
using System.Text.RegularExpressions;
using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>The final <c>result</c> of <c>claude -p</c>, read for the one thing Scion wants: the skill file.</summary>
internal abstract partial record ClaudeCodeAnswer
{
    private ClaudeCodeAnswer()
    {
    }

    public sealed record SkillFile(string Text) : ClaudeCodeAnswer;

    public sealed record UsageLimit : ClaudeCodeAnswer;

    public sealed record Error(string Message) : ClaudeCodeAnswer;

    public static ClaudeCodeAnswer Read(ProcessOutcome outcome)
    {
        JsonElement envelope;
        try
        {
            using var document = JsonDocument.Parse(outcome.StandardOutput);
            envelope = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new Error(FirstLineOf(outcome.StandardError, outcome.StandardOutput));
        }

        if (envelope.ValueKind != JsonValueKind.Object)
        {
            return new Error(FirstLineOf(outcome.StandardError, outcome.StandardOutput));
        }

        var result = TextOf(envelope, "result");
        var failed = outcome.ExitCode != 0 || (envelope.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True);

        if (!failed)
        {
            return SkillFileOf(envelope, result);
        }

        var isRateLimited = envelope.TryGetProperty("api_error_status", out var status) && status.ValueKind == JsonValueKind.Number && status.GetInt32() == 429;

        return isRateLimited || (result is not null && LimitMessage().IsMatch(result))
            ? new UsageLimit()
            : new Error(FirstLineOf(result, outcome.StandardError));
    }

    /// <summary>The structured output; the plain result only when there is no structured output at all.</summary>
    private static ClaudeCodeAnswer SkillFileOf(JsonElement envelope, string? result)
    {
        var text = envelope.TryGetProperty("structured_output", out var structured) && structured.ValueKind == JsonValueKind.Object
            ? TextOf(structured, "skillFile")
            : result;

        return string.IsNullOrWhiteSpace(text) ? new Error("Claude Code returned no skill file.") : new SkillFile(text);
    }

    private static string? TextOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string FirstLineOf(params string?[] candidates)
    {
        var line = candidates
            .Select(candidate => candidate?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault())
            .FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate));

        return line ?? "Claude Code did not finish.";
    }

    [GeneratedRegex(@"hit your .{0,40}?limit", RegexOptions.IgnoreCase)]
    private static partial Regex LimitMessage();
}
