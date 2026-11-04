using Scion.Application.Projects;

namespace Scion.Application.Tests.Projects;

internal sealed class InMemoryProjectRegistryRecovery : IProjectRegistryRecovery
{
    public const string BackupPath = "/home/.scion/projects.json.corrupt";

    public DateTimeOffset? SetAsideAt { get; private set; }

    public bool FailsToWrite { get; set; }

    public Task<string?> SetAsideAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (FailsToWrite)
        {
            throw new ProjectRegistryWriteFailedException(
                InMemoryProjectRegistry.RegistryFile, new IOException("locked"));
        }

        SetAsideAt = now;
        return Task.FromResult<string?>(BackupPath);
    }
}
