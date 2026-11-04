using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Skills.Copying;

internal static class LocationRoots
{
    /// <summary>Personal has no folder that can go missing; a project does.</summary>
    public static bool Exist(this IFolderProbe folderProbe, Location location) => location switch
    {
        Location.Personal => true,
        Location.Project project => folderProbe.Exists(project.Path),
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, null),
    };
}
