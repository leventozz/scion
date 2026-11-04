namespace Scion.Application.Skills;

public sealed record SkillSummary(
    string Name, string FolderName, string Description, IReadOnlyList<SkillWarning> Warnings, bool CanBeDeleted);
