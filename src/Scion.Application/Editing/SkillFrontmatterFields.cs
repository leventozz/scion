namespace Scion.Application.Editing;

/// <param name="IsUnreadable">The frontmatter is invalid YAML or its block never closes; name and description are then null.</param>
public sealed record SkillFrontmatterFields(string? Name, string? Description, bool IsUnreadable);
