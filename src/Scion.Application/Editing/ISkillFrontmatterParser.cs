namespace Scion.Application.Editing;

public interface ISkillFrontmatterParser
{
    /// <summary>Reads the frontmatter of a SKILL.md text. It does no I/O and never throws for bad YAML.</summary>
    SkillFrontmatterFields Parse(string text);
}
