namespace Scion.Application.Skills;

/// <param name="IsLinked">The folder, or a folder above it that holds skills, is a symlink or junction.</param>
public sealed record SkillFolder(
    string FolderName,
    string? Name,
    string? Description,
    bool FrontmatterUnreadable = false,
    bool IsLinked = false);
