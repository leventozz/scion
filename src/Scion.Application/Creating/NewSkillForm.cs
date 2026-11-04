using Scion.Application.Skills.Copying;
using Scion.Domain.Skills;

namespace Scion.Application.Creating;

/// <summary>
/// The rules of the New skill dialog for one place: the Domain's rules for name and description, plus "no folder of
/// that name is there yet". Pure, so the dialog can check on every keystroke.
/// </summary>
public sealed class NewSkillForm(IReadOnlyList<string> existingFolderNames, PathComparison pathComparison)
{
    public NewSkillCheck Check(string name, string description) =>
        new(CheckName(name), CheckDescription(description));

    private NewSkillProblem? CheckName(string name)
    {
        var result = SkillName.Create(name);
        if (!result.IsSuccess)
        {
            return ProblemOf(result.Error);
        }

        return existingFolderNames.Any(existing => pathComparison.Same(existing, name)) ? NewSkillProblem.NameTaken : null;
    }

    private static NewSkillProblem? CheckDescription(string description)
    {
        var result = SkillDescription.Create(NewSkillDescription.Normalize(description));

        return result.IsSuccess ? null : ProblemOf(result.Error);
    }

    private static NewSkillProblem ProblemOf(SkillNameError error) => error switch
    {
        SkillNameError.Empty => NewSkillProblem.NameEmpty,
        SkillNameError.TooLong => NewSkillProblem.NameTooLong,
        SkillNameError.UppercaseLetter or SkillNameError.DisallowedCharacter => NewSkillProblem.NameInvalidCharacters,
        SkillNameError.LeadingOrTrailingHyphen => NewSkillProblem.NameHyphenAtEdge,
        SkillNameError.ConsecutiveHyphens => NewSkillProblem.NameDoubleHyphen,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    private static NewSkillProblem ProblemOf(SkillDescriptionError error) => error switch
    {
        SkillDescriptionError.Empty => NewSkillProblem.DescriptionEmpty,
        SkillDescriptionError.TooLong => NewSkillProblem.DescriptionTooLong,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
