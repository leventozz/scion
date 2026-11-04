using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class RemoveProject(IProjectRegistry registry)
{
    public Task<Result<RemovedProject, RegistryFailure>> ExecuteAsync(
        string path, CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(async () =>
        {
            await registry.RemoveAsync(path, cancellationToken);
            return new RemovedProject(path);
        });
}
