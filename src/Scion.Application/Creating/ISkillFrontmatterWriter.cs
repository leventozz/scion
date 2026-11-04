using Scion.Domain.Skills;

namespace Scion.Application.Creating;

public interface ISkillFrontmatterWriter
{
    /// <summary>The <c>---</c> delimited YAML block of a SKILL.md, LF line endings, ending with a line break.</summary>
    string Write(SkillName name, SkillDescription description);
}
