using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

internal static class LocationRoot
{
    /// <summary>The home directory is the root of Personal; a project is its own root.</summary>
    public static string Of(Location location, string homeDirectory) => location switch
    {
        Location.Personal => homeDirectory,
        Location.Project project => project.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, null),
    };
}
