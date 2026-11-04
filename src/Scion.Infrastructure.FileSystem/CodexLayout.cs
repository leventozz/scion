using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class CodexLayout : IAgentLayout, IInstructionFileLayout
{
    public Agent Agent => Agent.Codex;

    public IReadOnlyList<string> ProjectInstructionFiles { get; } = ["AGENTS.md"];

    public IReadOnlyList<string> PersonalInstructionFiles { get; } = [".codex/AGENTS.md"];

    public string SkillsDirectoryUnder(string locationRoot) =>
        Path.Combine(locationRoot, ".agents", "skills");
}
