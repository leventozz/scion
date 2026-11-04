using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class StartFresh(IProjectRegistryRecovery recovery, TimeProvider time)
{
    /// <summary>Sets the unreadable registry aside as a backup. Returns the backup path, or null when there was nothing to set aside.</summary>
    public Task<Result<string?, RegistryFailure>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(() => recovery.SetAsideAsync(time.GetUtcNow(), cancellationToken));
}
