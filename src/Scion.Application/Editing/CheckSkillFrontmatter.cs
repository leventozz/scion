using Scion.Application.Skills;

namespace Scion.Application.Editing;

/// <summary>Validates the header of a SKILL.md draft with the rules the skill list uses.</summary>
public sealed class CheckSkillFrontmatter(ISkillFrontmatterParser parser)
{
    public SkillSummary Execute(string text, string folderName)
    {
        var fields = parser.Parse(text);

        return SkillValidation.Summarize(new SkillFolder(folderName, fields.Name, fields.Description, fields.IsUnreadable));
    }
}
