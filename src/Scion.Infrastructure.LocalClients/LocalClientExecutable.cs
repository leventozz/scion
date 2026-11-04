namespace Scion.Infrastructure.LocalClients;

/// <summary>
/// Finds the user's installed client program (Claude Code's <c>claude</c>, say). An explicit path (a test seam) is the
/// only place looked at when given. Otherwise the search path first, then the folders the client's installers use. It is
/// searched on every call, so a client installed while Scion runs is found.
/// </summary>
/// <param name="ProgramName">The name of the program without an extension.</param>
/// <param name="ExplicitPath">Where the program is, when the host knows; null searches.</param>
/// <param name="KnownFolders">Folders the installers use, looked at in this order after the search path.</param>
/// <param name="Environment">Reads an environment variable; the host passes the real one.</param>
public sealed class LocalClientExecutable(
    string ProgramName, string? ExplicitPath, IReadOnlyList<string> KnownFolders, Func<string, string?> Environment)
{
    private static readonly string[] WindowsExtensions = [".exe", ".cmd"];

    /// <summary>The path of the program, or null when it is not installed.</summary>
    public string? Locate()
    {
        if (!string.IsNullOrEmpty(ExplicitPath))
        {
            return File.Exists(ExplicitPath) ? ExplicitPath : null;
        }

        var fileNames = FileNames().ToList();

        return Folders()
            .SelectMany(folder => fileNames.Select(fileName => Path.Combine(folder, fileName)))
            .FirstOrDefault(File.Exists);
    }

    private IEnumerable<string> Folders()
    {
        var onSearchPath = (Environment("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return onSearchPath.Concat(KnownFolders);
    }

    /// <summary>On Windows the ones PATHEXT allows, the exe first: the cmd of an npm install comes second.</summary>
    private IEnumerable<string> FileNames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [ProgramName];
        }

        var allowed = (Environment("PATHEXT") ?? ".EXE;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries);

        return WindowsExtensions
            .Where(extension => allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .Select(extension => ProgramName + extension);
    }
}
