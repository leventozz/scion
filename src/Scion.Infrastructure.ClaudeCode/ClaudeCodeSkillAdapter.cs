using Scion.Application.Adapting;
using Scion.Domain;
using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Adapts a skill by running the user's own Claude Code in print mode: the instructions as the system prompt, the shared
/// documents on standard input, no tools, no MCP, no session. The answer is read as a stream, so the user can watch the
/// skill file being written; what counts is only the final <c>result</c> line. <c>--safe-mode</c> keeps every customization (CLAUDE.md,
/// skills, hooks, auto-memory) out, so what Claude sees is exactly the prompt. <c>--bare</c> is not used: it would not
/// read the user's sign-in.
/// </summary>
public sealed class ClaudeCodeSkillAdapter(ClaudeCodeExecutable executable, IAiProviderStatus status) : ISkillAdapter
{
    public async Task<Result<string, AdaptationFailure>> AdaptAsync(
        AdaptationPrompt prompt, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return Failure(new AdaptationFailure.NotInstalled());
        }

        try
        {
            var stream = new ClaudeCodeStream(progress);
            var outcome = await LocalClientProcess.RunAsync(
                path, ArgumentsFor(prompt), prompt.Content, OutputHandling.Lines(stream.Read), cancellationToken);

            // Without a result line (the stream ended early) the answer reads as "did not finish": streamed text is never a result.
            return outcome is null
                ? Failure(new AdaptationFailure.NotInstalled())
                : await ResultOfAsync(
                    ClaudeCodeAnswer.Read(outcome with { StandardOutput = stream.ResultLine ?? "" }), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(new AdaptationFailure.Cancelled());
        }
    }

    private static string[] ArgumentsFor(AdaptationPrompt prompt) =>
    [
        "-p",
        "--output-format", "stream-json",
        "--verbose",
        "--include-partial-messages",
        "--json-schema", AdaptationPrompt.AnswerSchema,
        "--tools", "",
        "--disallowedTools", "mcp__*",
        "--strict-mcp-config",
        "--safe-mode",
        "--no-session-persistence",
        "--system-prompt", prompt.Instructions,
    ];

    private async Task<Result<string, AdaptationFailure>> ResultOfAsync(ClaudeCodeAnswer answer, CancellationToken cancellationToken)
    {
        switch (answer)
        {
            case ClaudeCodeAnswer.SkillFile skillFile:
                return Result<string, AdaptationFailure>.Success(skillFile.Text);
            case ClaudeCodeAnswer.UsageLimit:
                return Failure(new AdaptationFailure.UsageLimitReached());
            case ClaudeCodeAnswer.Error error:
                // Not signed in is the one failure the user can fix at once, so it is told apart from the rest.
                return await status.GetStateAsync(cancellationToken) switch
                {
                    AiProviderState.SignedOut => Failure(new AdaptationFailure.SignedOut()),
                    AiProviderState.NotInstalled => Failure(new AdaptationFailure.NotInstalled()),
                    _ => Failure(new AdaptationFailure.Failed(error.Message)),
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(answer));
        }
    }

    private static Result<string, AdaptationFailure> Failure(AdaptationFailure failure) =>
        Result<string, AdaptationFailure>.Failure(failure);
}
