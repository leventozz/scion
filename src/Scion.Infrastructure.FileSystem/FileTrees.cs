namespace Scion.Infrastructure.FileSystem;

internal static class FileTrees
{
    /// <summary>Copies the tree file by file. A link inside it is copied as the content it points to.</summary>
    public static void Copy(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            Copy(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    /// <summary>
    /// Removes the tree from the bottom up, read-only files included. A link inside it is removed itself; what it
    /// points to stays.
    /// </summary>
    public static void Delete(string directory)
    {
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (FolderLinks.IsLink(child))
            {
                Directory.Delete(child);
            }
            else
            {
                Delete(child);
            }
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        File.SetAttributes(directory, FileAttributes.Normal);
        Directory.Delete(directory);
    }
}
