namespace Scion.Application.Projects;

public sealed record ProjectSummary(
    string Path, string DisplayName, int ClaudeSkillCount, int CodexSkillCount, bool FolderMissing = false);
