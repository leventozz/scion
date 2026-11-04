namespace Scion.Domain.Skills;

public enum SkillNameError
{
    Empty,
    TooLong,
    UppercaseLetter,
    LeadingOrTrailingHyphen,
    ConsecutiveHyphens,
    DisallowedCharacter,
}
