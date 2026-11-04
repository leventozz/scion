using System.Diagnostics;

namespace Scion.Infrastructure.FileSystem.Tests;

internal static class DirectoryLinks
{
    /// <summary>
    /// Makes <paramref name="link"/> point at <paramref name="target"/>: a junction on Windows (needs no privilege),
    /// a symbolic link elsewhere.
    /// </summary>
    public static void Create(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);

        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        mklink.WaitForExit();

        if (mklink.ExitCode != 0)
        {
            throw new InvalidOperationException($"mklink failed: {mklink.StandardError.ReadToEnd()}");
        }
    }
}
