using Scion.Application.Creating;

namespace Scion.UI;

internal static class NewSkillMessages
{
    public const string CreateFailed = "The skill couldn't be created.";

    /// <summary>The line under the field; null when the field is only not filled in yet.</summary>
    public static string? For(NewSkillProblem problem) => problem switch
    {
        NewSkillProblem.NameEmpty or NewSkillProblem.DescriptionEmpty => null,
        NewSkillProblem.NameInvalidCharacters => "Use lowercase letters, numbers and hyphens.",
        NewSkillProblem.NameTooLong => "Use at most 64 characters.",
        NewSkillProblem.NameHyphenAtEdge => "Don't start or end with a hyphen.",
        NewSkillProblem.NameDoubleHyphen => "Don't use two hyphens in a row.",
        NewSkillProblem.NameTaken => "A skill with this name already exists here.",
        NewSkillProblem.DescriptionTooLong => "Use at most 1024 characters.",
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null),
    };
}
