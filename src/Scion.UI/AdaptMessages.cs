using Scion.Application.Adapting;

namespace Scion.UI;

/// <summary>
/// The words of "Adapt with AI". It states only what Scion itself does (what it sends, that it hands the work to the
/// user's own client); it says nothing about what a provider does with the data. The one line about cost is the
/// provider's own <see cref="AiProvider.UsageNotes"/>.
/// </summary>
internal static class AdaptMessages
{
    public const string HowToInstall = "How to install";

    public const string WaitingForSignIn = "Finish signing in in your browser…";

    public const string NeverShared = "Source code is never shared. Scripts and references are copied unchanged.";

    public const string CopiedUnchanged = "Scripts and references will be copied unchanged.";

    public static string SignIn(AiProvider provider) => $"Sign in with {provider.SignInWith}";

    public static string Running(string skillName, string targetName) => $"Adapting {skillName} for {targetName}…";

    public static string AlreadyExists(string targetName) => $"A skill with this name already exists in {targetName}.";

    /// <summary>The size shown next to a shared file: whole kilobytes, rounded up, at least one.</summary>
    public static string SizeOf(long bytes) => $"{Math.Max(1, (bytes + 1023) / 1024)} KB";

    public static string For(SharedItem item) => item.Bytes is { } bytes ? $"{item.Label} · {SizeOf(bytes)}" : item.Label;

    public static string For(SharedSummaryLine line) => line switch
    {
        SharedSummaryLine.Item item => For(item.Value),
        SharedSummaryLine.ProjectFiles files => $"{files.Count} project {(files.Count == 1 ? "file" : "files")} · {SizeOf(files.Bytes)}",
        _ => throw new ArgumentOutOfRangeException(nameof(line), line, null),
    };

    /// <summary>A line of "Not shared": what was left out and the limit that left it out.</summary>
    public static string For(OmittedItem item) => item.Reason switch
    {
        OmittedReason.OverFileLimit over => $"Not shared (over {SizeOf(over.LimitBytes)}): {item.Label}",
        OmittedReason.TotalLimitReached => $"Not shared (total limit reached): {item.Label}",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    /// <summary>How long an adaptation has been running: minutes and two-digit seconds, never negative.</summary>
    public static string Elapsed(TimeSpan span)
    {
        var seconds = (int)Math.Max(0, span.TotalSeconds);

        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>The line under the dialog for a catalog of one provider, where nothing else says the provider's name.</summary>
    public static string ProviderLine(AiProvider provider, AiProviderState state) => state switch
    {
        AiProviderState.Ready => $"{provider.Name} · {ProviderDetail(provider, state)}",
        _ => ProviderDetail(provider, state),
    };

    /// <summary>What is said next to the name of the provider, which is shown beside it (the picker's trigger).</summary>
    public static string ProviderDetail(AiProvider provider, AiProviderState state) => state switch
    {
        AiProviderState.Ready => $"Connected via {provider.ClientName}",
        AiProviderState.NotInstalled => $"{provider.ClientName} isn't installed.",
        _ => $"Sign in to {provider.ClientName} to continue.",
    };

    /// <summary>How a provider is doing, in a few words, in the list the user picks a provider from.</summary>
    public static string ProviderState(AiProviderState state, AiProvider provider) => state switch
    {
        AiProviderState.Ready => $"Connected via {provider.ClientName}",
        AiProviderState.NotInstalled => "Not installed",
        _ => "Sign in needed",
    };

    /// <summary>A file the provider's client loads by itself: listed with what is shared, though Scion does not send it.</summary>
    public static string LoadedBy(ProviderLoadedFile file, AiProvider provider) =>
        $"~/{file.HomeRelativePath} (loaded by {provider.Name}) · {SizeOf(file.Bytes)}";

    public static string For(AdaptationFailure failure, AiProvider provider) => failure switch
    {
        AdaptationFailure.NotInstalled => ProviderLine(provider, AiProviderState.NotInstalled),
        AdaptationFailure.SignedOut => ProviderLine(provider, AiProviderState.SignedOut),
        AdaptationFailure.Cancelled => "",
        AdaptationFailure.TimedOut => $"{provider.ClientName} took too long, so Scion stopped it. Try again.",
        AdaptationFailure.UsageLimitReached => $"{provider.ClientName} couldn't finish: your usage limit was reached. Try again later.",
        AdaptationFailure.Failed failed => $"{provider.ClientName} couldn't finish: {failed.Message}",
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    public static string For(PrepareAdaptationError error) => error switch
    {
        PrepareAdaptationError.SameSourceAndTarget => "Choose a different project or agent.",
        PrepareAdaptationError.TargetFolderMissing => "This folder no longer exists.",
        _ => "Couldn't read SKILL.md.",
    };

    public static string For(SaveAdaptedSkillError error) => error switch
    {
        SaveAdaptedSkillError.TargetFolderMissing => "Couldn't save: the folder no longer exists.",
        _ => "Couldn't save the skill.",
    };
}
