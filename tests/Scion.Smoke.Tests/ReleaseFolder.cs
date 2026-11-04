using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scion.Smoke.Tests;

/// <summary>
/// A folder laid out the way Velopack publishes releases (<c>releases.win.json</c> and the packages it lists), written by hand.
/// It is the update source of the app under test: no test asks GitHub.
/// </summary>
internal sealed class ReleaseFolder : IDisposable
{
    private readonly TempDirectory _folder = new("scion-releases");
    private readonly List<object> _assets = [];

    public string Path => _folder.Path;

    public void Dispose() => _folder.Dispose();

    public string PackageOf(string version) => System.IO.Path.Combine(Path, $"Scion-{version}-full.nupkg");

    public void Publish(string version, string? notes = null)
    {
        var content = Encoding.UTF8.GetBytes($"package {version}");

        File.WriteAllBytes(PackageOf(version), content);
        _assets.Add(new
        {
            PackageId = "Scion",
            Version = version,
            Type = "Full",
            FileName = $"Scion-{version}-full.nupkg",
            SHA1 = Convert.ToHexString(SHA1.HashData(content)),
            SHA256 = Convert.ToHexString(SHA256.HashData(content)),
            Size = content.Length,
            NotesMarkdown = notes,
        });
        File.WriteAllText(System.IO.Path.Combine(Path, "releases.win.json"), JsonSerializer.Serialize(new { Assets = _assets }));
    }
}
