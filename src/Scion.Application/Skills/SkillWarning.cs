namespace Scion.Application.Skills;

public enum SkillWarning
{
    NameEmpty,
    NameTooLong,
    NameHasUppercaseLetter,
    NameHasLeadingOrTrailingHyphen,
    NameHasConsecutiveHyphens,
    NameHasDisallowedCharacter,
    NameDoesNotMatchFolder,
    DescriptionMissing,
    DescriptionTooLong,
    FrontmatterUnreadable,
}
