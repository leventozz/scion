using Scion.Domain;

namespace Scion.Application.Projects;

/// <summary>The places Scion looks after when the app starts: Personal and every registered project that is still on disk.</summary>
public sealed class StartupLocations(IProjectRegistry registry, IFolderProbe folderProbe)
{
    public async Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken)
    {
        var locations = new List<Location> { new Location.Personal() };

        try
        {
            locations.AddRange((await registry.ListAsync(cancellationToken))
                .Where(project => folderProbe.Exists(project.Path))
                .Select(project => new Location.Project(project.Path)));
        }
        catch (ProjectRegistryException)
        {
            // Without the project list there is still Personal to look after.
        }

        return locations;
    }
}
