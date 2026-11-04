using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scion.FakeCodex;

/// <summary>
/// What the fake does, read from the JSON file named by <c>SCION_FAKE_CODEX_SCRIPT</c>. The tests write it; the fake
/// never calls anything real.
/// </summary>
internal sealed class FakeCodexScript
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Whether <c>login status</c> says signed in from the start.</summary>
    public bool SignedIn { get; init; }

    /// <summary>How long <c>login</c> takes before the user counts as signed in; -1 never finishes.</summary>
    public int LoginDelayMs { get; init; }

    public ExecAnswer Exec { get; init; } = new();

    /// <summary>One JSON line per call is appended here: arguments, standard input, working directory and what the flags point at.</summary>
    public string? LogPath { get; init; }

    /// <summary>Starts a child process of its own (its id goes to <c>PidPath + ".child"</c>) before it answers.</summary>
    public bool SpawnChild { get; init; }

    /// <summary>How long it waits before it starts the child.</summary>
    public int SpawnChildAfterMs { get; init; } = 300;

    /// <summary>The process id of the fake is written here, so a test can check that it was ended.</summary>
    public string? PidPath { get; init; }

    public static FakeCodexScript Read(string path) =>
        JsonSerializer.Deserialize<FakeCodexScript>(File.ReadAllText(path), Options)!;

    public sealed class ExecAnswer
    {
        public ExecKind Kind { get; init; } = ExecKind.Success;

        public string SkillFile { get; init; } = "";

        /// <summary>The text of the failure or of the message that is not a skill file.</summary>
        public string Message { get; init; } = "";

        /// <summary>Wait before the answer (the time Codex thinks).</summary>
        public int DelayMs { get; init; }
    }

    public enum ExecKind
    {
        /// <summary>The events of a real run: the code-mode warning item, then the agent message with <c>{"skillFile": …}</c>.</summary>
        Success,

        /// <summary>A command ran (<c>command_execution</c>, completed), then it never goes on.</summary>
        ToolRan,

        /// <summary>Only warning items (the tool was blocked) and the answer, as if no tool had run.</summary>
        ToolBlocked,

        /// <summary>A <c>turn.failed</c> with <see cref="ExecAnswer.Message"/>; exit code 1.</summary>
        TurnFailed,

        /// <summary>A <c>turn.failed</c> whose text says the usage limit was hit; exit code 1.</summary>
        UsageLimit,

        /// <summary>An agent message that is not JSON.</summary>
        NotJson,

        /// <summary>No event at all, a line on standard error and exit code 1.</summary>
        CrashWithoutAnswer,

        /// <summary>Never answers.</summary>
        Hang,
    }
}
