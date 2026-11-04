using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scion.FakeClaude;

/// <summary>
/// What the fake does, read from the JSON file named by <c>SCION_FAKE_CLAUDE_SCRIPT</c>. The tests write it; the fake
/// never calls anything real.
/// </summary>
internal sealed class FakeClaudeScript
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Whether <c>auth status</c> says signed in from the start.</summary>
    public bool SignedIn { get; init; }

    /// <summary>The <c>authMethod</c> that <c>auth status</c> reports; null leaves it out.</summary>
    public string? AuthMethod { get; init; }

    /// <summary>Whether <c>auth status</c> also prints account and organization, as the real one does.</summary>
    public bool ExtraAuthFields { get; init; }

    /// <summary>Whether <c>auth status</c> prints text that is not JSON.</summary>
    public bool AuthStatusNotJson { get; init; }

    /// <summary>How long <c>auth login</c> takes before the user counts as signed in; -1 never finishes.</summary>
    public int LoginDelayMs { get; init; }

    public AdaptAnswer Adapt { get; init; } = new();

    /// <summary>One JSON line per call is appended here: arguments, standard input, working directory and environment facts.</summary>
    public string? LogPath { get; init; }

    /// <summary>Starts a child process of its own (its id goes to <c>PidPath + ".child"</c>) before it answers.</summary>
    public bool SpawnChild { get; init; }

    /// <summary>How long it waits before it starts the child.</summary>
    public int SpawnChildAfterMs { get; init; } = 300;

    /// <summary>The process id of the fake is written here, so a test can check that it was ended.</summary>
    public string? PidPath { get; init; }

    public static FakeClaudeScript Read(string path) =>
        JsonSerializer.Deserialize<FakeClaudeScript>(File.ReadAllText(path), Options)!;

    public sealed class AdaptAnswer
    {
        public AdaptKind Kind { get; init; } = AdaptKind.Success;

        public string SkillFile { get; init; } = "";

        public string Message { get; init; } = "";

        /// <summary>For <see cref="AdaptKind.Success"/>: the skill file JSON is written in pieces of this many characters; 0 is one piece.</summary>
        public int ChunkSize { get; init; }

        /// <summary>Wait before the first piece (the time Claude Code thinks before it writes anything).</summary>
        public int FirstChunkDelayMs { get; init; }

        /// <summary>Wait after every piece, so a test can look at the screen while the text is arriving.</summary>
        public int ChunkDelayMs { get; init; }

        /// <summary>Ends with exit code 1 and no result after this many pieces (a stream that stops early).</summary>
        public int? StopAfterChunks { get; init; }

        /// <summary>Never goes on after this many pieces (to cancel in the middle of a stream).</summary>
        public int? HangAfterChunks { get; init; }
    }

    public enum AdaptKind
    {
        /// <summary>A stream that writes the skill file piece by piece, then a result with <c>structured_output.skillFile</c>.</summary>
        Success,

        /// <summary>A result with the skill file in <c>result</c> only.</summary>
        ResultOnly,

        /// <summary>An error with <c>api_error_status</c> 429.</summary>
        LimitByStatus,

        /// <summary>An error whose text says the usage limit was hit.</summary>
        LimitByText,

        /// <summary>An error with <see cref="AdaptAnswer.Message"/> as its text.</summary>
        Error,

        /// <summary>Output that is not JSON at all.</summary>
        NotJson,

        /// <summary>Never answers.</summary>
        Hang,
    }
}
