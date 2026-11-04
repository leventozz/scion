using Scion.Domain.Skills;

namespace Scion.Application.Skills;

internal static class SkillWarnings
{
    public static SkillWarning From(SkillNameError error) => error switch
    {
        SkillNameError.Empty => SkillWarning.NameEmpty,
        SkillNameError.TooLong => SkillWarning.NameTooLong,
        SkillNameError.UppercaseLetter => SkillWarning.NameHasUppercaseLetter,
        SkillNameError.LeadingOrTrailingHyphen => SkillWarning.NameHasLeadingOrTrailingHyphen,
        SkillNameError.ConsecutiveHyphens => SkillWarning.NameHasConsecutiveHyphens,
        SkillNameError.DisallowedCharacter => SkillWarning.NameHasDisallowedCharacter,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    public static SkillWarning From(SkillDescriptionError error) => error switch
    {
        SkillDescriptionError.Empty => SkillWarning.DescriptionMissing,
        SkillDescriptionError.TooLong => SkillWarning.DescriptionTooLong,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
