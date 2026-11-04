using Scion.Application.Adapting;

namespace Scion.Infrastructure.Codex;

public static class CodexProvider
{
    /// <summary>
    /// The usage note is one fixed line: <c>codex exec</c> uses an API key from the environment (<c>CODEX_API_KEY</c>) instead of
    /// the sign-in when there is one, and what Codex reports about the sign-in does not say which of the two a run is paid with.
    /// </summary>
    public static readonly AiProvider Codex = new(
        new AiProviderId("codex"),
        "Codex",
        "Codex CLI",
        "ChatGPT",
        new Uri("https://developers.openai.com/codex/cli"),
        AiUsageNotes.Always("Counts toward your ChatGPT plan unless an API key is set for Codex."));
}
