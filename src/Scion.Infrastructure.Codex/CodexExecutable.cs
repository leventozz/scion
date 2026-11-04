using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.Codex;

/// <summary>
/// Finds the user's <c>codex</c> program: an explicit path (the test seam <c>SCION_CODEX_PATH</c>) when given, else the
/// search path, then the folders the Codex installers use. How the search works is <see cref="LocalClientExecutable"/>'s
/// business; what is Codex's own is the name and the folders. The Windows folders are the ones seen on a Windows 11
/// install (the app's <c>bin</c> folder and npm's global folder); the other systems' folders are the usual ones and have not
/// been checked.
/// </summary>
/// <param name="HomeDirectory">The home folder the per-user install folders are under.</param>
/// <param name="Environment">Reads an environment variable; the host passes the real one.</param>
public sealed class CodexExecutable(string? ExplicitPath, string HomeDirectory, Func<string, string?> Environment)
{
    private static readonly string[] UnixFolders = ["/opt/homebrew/bin", "/usr/local/bin"];

    private readonly LocalClientExecutable _executable = new(
        "codex", ExplicitPath, KnownFolders(HomeDirectory), Environment);

    /// <summary>The path of the program, or null when Codex is not installed.</summary>
    public string? Locate() => _executable.Locate();

    private static string[] KnownFolders(string homeDirectory) =>
        OperatingSystem.IsWindows()
            ?
            [
                Path.Combine(homeDirectory, "AppData", "Local", "Programs", "OpenAI", "Codex", "bin"),
                Path.Combine(homeDirectory, "AppData", "Roaming", "npm"),
            ]
            : [Path.Combine(homeDirectory, ".local", "bin"), .. UnixFolders];
}
