using Scion.Domain;

namespace Scion.Application.Projects;

public interface IProjectRegistry
{
    Task<IReadOnlyList<RegisteredProject>> ListAsync(CancellationToken cancellationToken);

    Task<RegisteredProject?> FindByPathAsync(string path, CancellationToken cancellationToken);

    /// <summary>Adds the project, or replaces the entry that has the same path.</summary>
    Task SaveAsync(RegisteredProject project, CancellationToken cancellationToken);

    /// <summary>Removes the entry that has this path. An unknown path is not an error.</summary>
    Task RemoveAsync(string path, CancellationToken cancellationToken);
}
