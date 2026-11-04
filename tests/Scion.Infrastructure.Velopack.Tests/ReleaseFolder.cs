using System.Security.Cryptography;
using System.Text.Json;

namespace Scion.Infrastructure.Velopack.Tests;

/// <summary>A folder laid out the way Velopack publishes releases (<c>releases.win.json</c> and the packages it lists), written by hand.</summary>
internal sealed class ReleaseFolder : IDisposable
{
    private readonly TempDirectory _folder = new();
    private readonly List<object> _assets = [];

    public string Path => _folder.Path;

    public void Dispose() => _folder.Dispose();

    public void Publish(string version, string? notes = null, byte[]? package = null)
    {
        var fileName = $"Scion-{version}-full.nupkg";
        var content = package ?? System.Text.Encoding.UTF8.GetBytes($"package {version}");

        File.WriteAllBytes(System.IO.Path.Combine(Path, fileName), content);
        _assets.Add(new
        {
            PackageId = "Scion",
            Version = version,
            Type = "Full",
            FileName = fileName,
            SHA1 = Convert.ToHexString(SHA1.HashData(content)),
            SHA256 = Convert.ToHexString(SHA256.HashData(content)),
            Size = content.Length,
            NotesMarkdown = notes,
        });
        WriteFeed(JsonSerializer.Serialize(new { Assets = _assets }));
    }

    public void WriteFeed(string content) => File.WriteAllText(System.IO.Path.Combine(Path, "releases.win.json"), content);
}
