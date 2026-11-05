namespace Scion.SiteCheck.Tests;

/// <summary>A scratch folder that stands for a website, removed when the test ends.</summary>
internal sealed class TempSite : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("scion-site-check-").FullName;

    public TempSite With(string relativePath, string content)
    {
        var file = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return this;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
