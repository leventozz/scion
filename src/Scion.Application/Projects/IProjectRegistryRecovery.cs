namespace Scion.Application.Projects;

public interface IProjectRegistryRecovery
{
    /// <summary>
    /// Moves the registry file out of the way so Scion can start with an empty registry.
    /// Never deletes or overwrites anything. Returns where the old file went, or null when there was no file.
    /// </summary>
    Task<string?> SetAsideAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
