using Scion.Application.Skills;

namespace Scion.UI;

internal static class SkillWarningMessages
{
    public static string For(SkillWarning warning, SkillSummary skill) => warning switch
    {
        SkillWarning.NameEmpty => "SKILL.md has no name.",
        SkillWarning.NameTooLong => "Name is longer than 64 characters.",
        SkillWarning.NameHasUppercaseLetter or SkillWarning.NameHasDisallowedCharacter =>
            "Name must use lowercase letters, numbers and hyphens.",
        SkillWarning.NameHasLeadingOrTrailingHyphen => "Name must not start or end with a hyphen.",
        SkillWarning.NameHasConsecutiveHyphens => "Name must not contain consecutive hyphens.",
        SkillWarning.NameDoesNotMatchFolder => $"Name doesn't match the folder name \"{skill.FolderName}\".",
        SkillWarning.DescriptionMissing => "Description is missing.",
        SkillWarning.DescriptionTooLong => "Description is longer than 1024 characters.",
        SkillWarning.FrontmatterUnreadable => "SKILL.md header could not be read.",
        _ => throw new ArgumentOutOfRangeException(nameof(warning), warning, null),
    };
}
