using Scion.Domain;

namespace Scion.UI;

internal static class AgentLabels
{
    public static string DisplayName(Agent agent) => agent switch
    {
        Agent.ClaudeCode => "Claude",
        Agent.Codex => "Codex",
        _ => throw new ArgumentOutOfRangeException(nameof(agent), agent, null),
    };

    public static string InstructionFileName(Agent agent) => agent switch
    {
        Agent.ClaudeCode => "CLAUDE.md",
        Agent.Codex => "AGENTS.md",
        _ => throw new ArgumentOutOfRangeException(nameof(agent), agent, null),
    };

    /// <summary>Personal skills are shown from the home folder, project skills from the project root.</summary>
    public static string SkillsPath(Location location, Agent agent) =>
        location is Location.Personal ? $"~/{SkillsFolder(agent)}" : SkillsFolder(agent);

    private static string SkillsFolder(Agent agent) => agent switch
    {
        Agent.ClaudeCode => ".claude/skills",
        Agent.Codex => ".agents/skills",
        _ => throw new ArgumentOutOfRangeException(nameof(agent), agent, null),
    };
}
