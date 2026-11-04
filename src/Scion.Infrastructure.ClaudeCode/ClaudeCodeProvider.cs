using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode;

public static class ClaudeCodeProvider
{
    public static readonly AiProvider Claude = new(
        new AiProviderId("claude"),
        "Claude",
        "Claude Code",
        "Claude",
        new Uri("https://code.claude.com/docs/en/setup"),
        new AiUsageNotes(Subscription: "Counts toward your Claude plan.", ApiKey: "Uses your Anthropic API key."));
}
