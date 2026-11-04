using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

internal sealed class InMemoryProjectRegistry : IProjectRegistry
{
    public const string RegistryFile = "/home/.scion/projects.json";

    private readonly List<RegisteredProject> _projects = [];

    public IReadOnlyList<RegisteredProject> Projects => _projects;

    public int SaveCount { get; private set; }

    /// <summary>When set, every call throws like the real adapter does for a file it cannot read.</summary>
    public bool IsUnreadable { get; set; }

    /// <summary>When set, calls that write throw like the real adapter does when saving fails.</summary>
    public bool FailsToWrite { get; set; }

    public void Add(RegisteredProject project) => _projects.Add(project);

    public Task<IReadOnlyList<RegisteredProject>> ListAsync(CancellationToken cancellationToken)
    {
        ThrowWhenUnreadable();
        return Task.FromResult<IReadOnlyList<RegisteredProject>>(_projects.ToList());
    }

    public Task<RegisteredProject?> FindByPathAsync(string path, CancellationToken cancellationToken)
    {
        ThrowWhenUnreadable();
        return Task.FromResult(_projects.SingleOrDefault(project => project.Path == path));
    }

    public Task SaveAsync(RegisteredProject project, CancellationToken cancellationToken)
    {
        ThrowWhenUnreadable();
        ThrowWhenWriteFails();
        SaveCount++;
        _projects.RemoveAll(existing => existing.Path == project.Path);
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string path, CancellationToken cancellationToken)
    {
        ThrowWhenUnreadable();
        ThrowWhenWriteFails();
        _projects.RemoveAll(existing => existing.Path == path);
        return Task.CompletedTask;
    }

    private void ThrowWhenUnreadable()
    {
        if (IsUnreadable)
        {
            throw new ProjectRegistryUnreadableException(RegistryFile);
        }
    }

    private void ThrowWhenWriteFails()
    {
        if (FailsToWrite)
        {
            throw new ProjectRegistryWriteFailedException(RegistryFile, new IOException("disk full"));
        }
    }
}
