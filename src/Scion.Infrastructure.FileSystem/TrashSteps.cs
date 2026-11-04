namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// The steps of a move to the trash that touch the disk in ways a test cannot provoke with real files: moving
/// a directory (also across volumes), copying a tree and removing one. Tests replace single steps.
/// </summary>
/// <param name="MoveDirectory">Throws <see cref="DirectoryMoveAcrossVolumesException"/> when only a copy can do it.</param>
internal sealed record TrashSteps(
    Action<string, string> MoveDirectory,
    Action<string, string> CopyTree,
    Action<string> DeleteTree)
{
    public static TrashSteps Real { get; } = new(MoveOnVolume, FileTrees.Copy, FileTrees.Delete);

    private static void MoveOnVolume(string source, string target)
    {
        // Windows names the problem before it moves anything; elsewhere the failed rename is the only hint.
        if (OperatingSystem.IsWindows() && !SameRoot(source, target))
        {
            throw new DirectoryMoveAcrossVolumesException();
        }

        try
        {
            Directory.Move(source, target);
        }
        catch (IOException exception) when (!OperatingSystem.IsWindows())
        {
            throw new DirectoryMoveAcrossVolumesException(exception);
        }
    }

    private static bool SameRoot(string left, string right) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(left)), Path.GetPathRoot(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
}
