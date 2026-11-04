namespace Scion.Infrastructure.FileSystem;

internal static class BestEffort
{
    /// <summary>For cleanup after the outcome is already decided: a failing cleanup is not an error.</summary>
    public static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void DeleteFile(string file) => Run(() =>
    {
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    });

    public static void DeleteDirectoryIfEmpty(string directory) => Run(() =>
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    });
}
