using Scion.Application.Projects;

namespace Scion.Application.Tests.Projects;

/// <summary>Every folder exists unless it is marked as missing.</summary>
internal sealed class InMemoryFolderProbe : IFolderProbe
{
    private readonly HashSet<string> _missing = [];

    public void MarkMissing(string path) => _missing.Add(path);

    public bool Exists(string path) => !_missing.Contains(path);
}
