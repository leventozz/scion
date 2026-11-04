using Scion.Domain;

namespace Scion.Application.Skills.Copying;

/// <summary>The file system's case rule, injected by the host, for comparing paths and folder names.</summary>
public sealed record PathComparison(StringComparison Value)
{
    public bool Same(string first, string second) => string.Equals(first, second, Value);

    public bool Same(Location first, Location second) => (first, second) switch
    {
        (Location.Personal, Location.Personal) => true,
        (Location.Project firstProject, Location.Project secondProject) => Same(firstProject.Path, secondProject.Path),
        _ => false,
    };
}
