using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;

namespace Scion.Application.Tests.Adapting;

/// <summary>Gives back whatever a test put in, and remembers what it was asked.</summary>
internal sealed class InMemoryTargetContext : ITargetContextReader
{
    private readonly List<TargetContextFile> _files = [];
    private readonly List<string> _folderNames = [];

    public AgentLocation? AskedFor { get; private set; }

    public ContextLimits? AskedWith { get; private set; }

    public void AddInstructionFile(string relativePath, string text) =>
        _files.Add(new TargetContextFile(relativePath, TargetContextFileKind.InstructionFile, Encoding.UTF8.GetBytes(text)));

    public void AddManifest(string relativePath, string text) => AddManifest(relativePath, Encoding.UTF8.GetBytes(text));

    public void AddManifest(string relativePath, byte[] bytes) =>
        _files.Add(new TargetContextFile(relativePath, TargetContextFileKind.Manifest, bytes));

    public void AddFolderNames(params string[] names) => _folderNames.AddRange(names);

    public Task<TargetContext> ReadAsync(AgentLocation target, ContextLimits limits, CancellationToken cancellationToken)
    {
        AskedFor = target;
        AskedWith = limits;

        return Task.FromResult(new TargetContext(_files, _folderNames));
    }
}
