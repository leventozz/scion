using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>Where the skills of an agent live in a location; shared by every adapter that touches skill folders.</summary>
internal sealed class SkillsDirectories(string homeDirectory, IEnumerable<IAgentLayout> layouts)
{
    public string For(Location location, Agent agent) =>
        layouts.Single(layout => layout.Agent == agent).SkillsDirectoryUnder(LocationRoot.Of(location, homeDirectory));
}
