using Scion.Infrastructure.LocalClients;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Finds the user's <c>claude</c> program: an explicit path (the test seam <c>SCION_CLAUDE_PATH</c>) when given, else the
/// search path, then the folders the Claude Code installers use. How the search works is <see cref="LocalClientExecutable"/>'s
/// business; what is Claude Code's own is the name and the folders.
/// </summary>
/// <param name="HomeDirectory">The home folder whose <c>.local/bin</c> the native installer uses.</param>
/// <param name="Environment">Reads an environment variable; the host passes the real one.</param>
public sealed class ClaudeCodeExecutable(string? ExplicitPath, string HomeDirectory, Func<string, string?> Environment)
{
    private static readonly string[] UnixFolders = ["/opt/homebrew/bin", "/usr/local/bin"];

    private readonly LocalClientExecutable _executable = new(
        "claude", ExplicitPath, KnownFolders(HomeDirectory), Environment);

    /// <summary>The path of the program, or null when Claude Code is not installed.</summary>
    public string? Locate() => _executable.Locate();

    private static string[] KnownFolders(string homeDirectory)
    {
        var local = Path.Combine(homeDirectory, ".local", "bin");

        return OperatingSystem.IsWindows() ? [local] : [local, .. UnixFolders];
    }
}
