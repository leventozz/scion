using Scion.Application.Projects;

namespace Scion.UI.Tests;

/// <summary>Every folder exists unless it is marked as missing.</summary>
internal sealed class InMemoryFolderProbe : IFolderProbe
{
    private readonly HashSet<string> _missing = [];

    public void MarkMissing(string path) => _missing.Add(path);

    public bool Exists(string path) => !_missing.Contains(path);
}
