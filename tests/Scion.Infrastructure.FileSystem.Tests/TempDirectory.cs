using System.Text;

namespace Scion.Infrastructure.FileSystem.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    private readonly List<string> _links = [];

    public string Path { get; }

    public void WriteFile(string relativePath, string content, Encoding? encoding = null)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, encoding ?? new UTF8Encoding(false));
    }

    /// <summary>Makes the folder at <paramref name="relativePath"/> a link to <paramref name="target"/>.</summary>
    public void LinkDirectory(string relativePath, string target)
    {
        var link = System.IO.Path.Combine(Path, relativePath);
        DirectoryLinks.Create(link, target);
        _links.Add(link);
    }

    public void Dispose()
    {
        // A recursive delete cannot remove a junction on Windows, so links go first (the target stays untouched).
        foreach (var link in _links.Where(link => new DirectoryInfo(link).LinkTarget is not null))
        {
            Directory.Delete(link);
        }

        Directory.Delete(Path, recursive: true);
    }
}
