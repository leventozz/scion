namespace Scion.UI;

/// <summary>
/// The words of updating. They state only what Scion itself does: which version there is, how far a download is, and what
/// failed. They say nothing about GitHub except where the user can fetch Scion by hand.
/// </summary>
internal static class UpdateMessages
{
    public const string WhatsNew = "What's new";

    public const string NoReleaseNotes = "No release notes.";

    public const string Later = "Later";

    public const string DownloadUpdate = "Download update";

    public const string Cancel = "Cancel";

    public const string RestartNow = "Restart now";

    public const string RestartToFinish = "Restart Scion to finish updating.";

    public const string FailedTitle = "Couldn't update";

    public const string Close = "Close";

    public const string TryAgain = "Try again";

    public const string DownloadFromGitHub = "Download from GitHub instead";

    public const string Checking = "Checking…";

    public const string UpToDate = "You're up to date.";

    public const string OnlyInstalledBuilds = "Updates are available in installed builds.";

    public const string CheckFailed = "Couldn't check for updates.";

    public const string CheckForUpdates = "Check for updates";

    public const string CheckWhenScionStarts = "Check for updates when Scion starts";

    public static string UpdateTo(string version) => $"Update to {version}";

    public static string Available(string version) => $"Scion {version} is available";

    public static string YouHave(string version) => $"You have {version}.";

    public static string Downloading(string version) => $"Downloading Scion {version}";

    public static string Ready(string version) => $"Scion {version} is ready";

    public static string Percent(int percent) => $"{percent}%";

    public static string ScionVersion(string version) => $"Scion {version}";

    public static string For(UpdateFailure failure) => failure switch
    {
        UpdateFailure.Download => "The download failed. Check your connection and try again.",
        UpdateFailure.Install => "Scion couldn't start the update. Try again, or download it from GitHub.",
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };
}
