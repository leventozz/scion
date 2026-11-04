using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public interface IAgentLayout
{
    Agent Agent { get; }

    string SkillsDirectoryUnder(string locationRoot);
}
