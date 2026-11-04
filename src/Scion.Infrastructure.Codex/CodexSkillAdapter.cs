using System.Text.RegularExpressions;
using Scion.Application.Adapting;
using Scion.Domain;
using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.Codex;

/// <summary>
/// Adapts a skill by running the user's own Codex in <c>exec</c> mode: instructions and shared documents on standard input,
/// every tool switched off, a read-only sandbox, no session, none of the user's config or rules, in a new empty folder. The
/// answer is the last agent message, shaped by an output schema. Codex does not stream the answer, so nothing is reported while
/// it works. What Scion cannot switch off by flag (Codex's own personal instruction file) is listed by <see cref="CodexOwnFiles"/>.
/// <para>
/// A tool that runs anyway ends the run and throws its result away (<see cref="CodexEvents"/>): the flags are meant to make that
/// impossible, so it is treated as a sign that they no longer hold.
/// </para>
/// </summary>
public sealed partial class CodexSkillAdapter(CodexExecutable executable, IAiProviderStatus status) : ISkillAdapter
{
    private const string ToolRanMessage = "Codex used a tool, so Scion stopped it and discarded the result.";

    /// <summary>
    /// Every feature that gives Codex something to act with, one flag each. Names as in Codex 0.160.0: a name a later version
    /// does not know is a flag that is still accepted, but one that has to be checked again when Codex is updated.
    /// </summary>
    private static readonly string[] DisabledFeatures =
    [
        "shell_tool", "unified_exec", "view_image", "apps", "plugins", "memories", "multi_agent", "browser_use", "computer_use",
        "image_generation", "hooks", "skill_search", "code_mode_host", "sleep_tool", "tool_suggest",
    ];

    public async Task<Result<string, AdaptationFailure>> AdaptAsync(
        AdaptationPrompt prompt, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken)
    {
        if (executable.Locate() is not { } path)
        {
            return Failure(new AdaptationFailure.NotInstalled());
        }

        // The schema is a file Codex reads, and it must not be in the folder Codex works in.
        var schemaDirectory = Directory.CreateTempSubdirectory("scion-codex-schema-");
        try
        {
            var schemaPath = Path.Combine(schemaDirectory.FullName, "skill.json");
            await File.WriteAllTextAsync(schemaPath, AdaptationPrompt.AnswerSchema, CancellationToken.None);

            return await RunAsync(path, prompt, schemaPath, cancellationToken);
        }
        finally
        {
            TryDelete(schemaDirectory);
        }
    }

    private async Task<Result<string, AdaptationFailure>> RunAsync(
        string path, AdaptationPrompt prompt, string schemaPath, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var events = new CodexEvents(stop.Cancel);

        try
        {
            var outcome = await LocalClientProcess.RunAsync(
                path,
                workingDirectory => ArgumentsFor(workingDirectory, schemaPath),
                prompt.Instructions + "\n\n" + prompt.Content,
                OutputHandling.Lines(events.Read),
                stop.Token);

            if (outcome is null)
            {
                return Failure(new AdaptationFailure.NotInstalled());
            }

            return events.ToolRan ? Failure(new AdaptationFailure.Failed(ToolRanMessage)) : await ResultOfAsync(events, outcome, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(new AdaptationFailure.Cancelled());
        }
        catch (OperationCanceledException) when (events.ToolRan)
        {
            return Failure(new AdaptationFailure.Failed(ToolRanMessage));
        }
    }

    private static List<string> ArgumentsFor(string workingDirectory, string schemaPath)
    {
        List<string> arguments =
        [
            "exec", "-",
            "--json",
            "--ephemeral",
            "--ignore-user-config",
            "--ignore-rules",
            "--skip-git-repo-check",
            "--sandbox", "read-only",
            "-C", workingDirectory,
        ];

        arguments.AddRange(DisabledFeatures.SelectMany(feature => new[] { "--disable", feature }));
        arguments.AddRange(["-c", "web_search=\"disabled\"", "--output-schema", schemaPath]);

        return arguments;
    }

    private async Task<Result<string, AdaptationFailure>> ResultOfAsync(
        CodexEvents events, ProcessOutcome outcome, CancellationToken cancellationToken)
    {
        if (events.TurnFailure is { } failure)
        {
            return await FailedTurnAsync(failure, cancellationToken);
        }

        if (outcome.ExitCode != 0)
        {
            return Failure(new AdaptationFailure.Failed(FirstLineOf(outcome.StandardError) ?? "Codex did not finish."));
        }

        return CodexAnswer.Read(events.LastAgentMessage) switch
        {
            CodexAnswer.SkillFile skillFile => Result<string, AdaptationFailure>.Success(skillFile.Text),
            _ => Failure(new AdaptationFailure.Failed("Codex returned no skill file.")),
        };
    }

    private async Task<Result<string, AdaptationFailure>> FailedTurnAsync(string message, CancellationToken cancellationToken)
    {
        if (UsageLimitMessage().IsMatch(message))
        {
            return Failure(new AdaptationFailure.UsageLimitReached());
        }

        // Not signed in is the one failure the user can fix at once, so it is told apart from the rest.
        return await status.GetStateAsync(cancellationToken) switch
        {
            AiProviderState.SignedOut => Failure(new AdaptationFailure.SignedOut()),
            AiProviderState.NotInstalled => Failure(new AdaptationFailure.NotInstalled()),
            _ => Failure(new AdaptationFailure.Failed(message)),
        };
    }

    private static string? FirstLineOf(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    private static Result<string, AdaptationFailure> Failure(AdaptationFailure failure) =>
        Result<string, AdaptationFailure>.Failure(failure);

    private static void TryDelete(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [GeneratedRegex(@"usage limit", RegexOptions.IgnoreCase)]
    private static partial Regex UsageLimitMessage();
}
