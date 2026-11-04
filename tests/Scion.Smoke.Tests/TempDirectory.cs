namespace Scion.Smoke.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // The window of the app that just ended may still hold a handle. Temp files are harmless.
        }
    }
}
