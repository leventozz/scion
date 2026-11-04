using Scion.Domain;

namespace Scion.UI;

internal static class InstructionFileMessages
{
    /// <summary>A project file is shown from the project root, a personal one from the home folder.</summary>
    public static string PathLabel(Location location, string relativePath) =>
        location is Location.Personal ? $"~/{relativePath}" : $"./{relativePath}";

    public static string Empty(Location location, Agent agent) => location is Location.Personal
        ? $"No personal {AgentLabels.InstructionFileName(agent)}"
        : $"No {AgentLabels.InstructionFileName(agent)} in this project";
}
