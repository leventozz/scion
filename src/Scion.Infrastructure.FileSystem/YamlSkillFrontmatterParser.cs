using Scion.Application.Editing;

namespace Scion.Infrastructure.FileSystem;

public sealed class YamlSkillFrontmatterParser : ISkillFrontmatterParser
{
    public SkillFrontmatterFields Parse(string text)
    {
        var frontmatter = SkillFrontmatterReader.Read(new StringReader(text));

        return new SkillFrontmatterFields(frontmatter.Name, frontmatter.Description, frontmatter.IsUnreadable);
    }
}
