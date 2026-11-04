using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;

namespace Scion.UI.Tests;

internal sealed class InMemoryTargetContext : ITargetContextReader
{
    private readonly List<TargetContextFile> _files = [];
    private readonly List<string> _folderNames = [];

    public void AddInstructionFile(string relativePath, string text) =>
        _files.Add(new TargetContextFile(relativePath, TargetContextFileKind.InstructionFile, Encoding.UTF8.GetBytes(text)));

    public void AddManifest(string relativePath, string text) =>
        _files.Add(new TargetContextFile(relativePath, TargetContextFileKind.Manifest, Encoding.UTF8.GetBytes(text)));

    public void AddFolderNames(params string[] names) => _folderNames.AddRange(names);

    public void Clear()
    {
        _files.Clear();
        _folderNames.Clear();
    }

    public Task<TargetContext> ReadAsync(AgentLocation target, ContextLimits limits, CancellationToken cancellationToken) =>
        Task.FromResult(new TargetContext(_files, _folderNames));
}
