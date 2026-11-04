using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class ClaudeCodeLayout : IAgentLayout, IInstructionFileLayout
{
    public Agent Agent => Agent.ClaudeCode;

    public IReadOnlyList<string> ProjectInstructionFiles { get; } = ["CLAUDE.md", ".claude/CLAUDE.md"];

    public IReadOnlyList<string> PersonalInstructionFiles { get; } = [".claude/CLAUDE.md"];

    public string SkillsDirectoryUnder(string locationRoot) =>
        Path.Combine(locationRoot, ".claude", "skills");
}
