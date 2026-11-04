using Scion.Application.Projects;

namespace Scion.UI.Tests;

/// <summary>Setting the registry aside leaves an empty, readable registry behind, like the real adapter.</summary>
internal sealed class InMemoryProjectRegistryRecovery(InMemoryProjectRegistry registry) : IProjectRegistryRecovery
{
    public const string BackupPath = "/home/me/.scion/projects.json.corrupt-20260501T120000Z";

    public int SetAsideCount { get; private set; }

    public bool FailsToWrite { get; set; }

    public Task<string?> SetAsideAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (FailsToWrite)
        {
            throw new ProjectRegistryWriteFailedException(
                InMemoryProjectRegistry.RegistryFile, new IOException("locked"));
        }

        SetAsideCount++;
        registry.IsUnreadable = false;
        registry.Clear();
        return Task.FromResult<string?>(BackupPath);
    }
}
