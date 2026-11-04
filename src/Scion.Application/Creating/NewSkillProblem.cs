namespace Scion.Application.Creating;

public enum NewSkillProblem
{
    NameEmpty,
    NameTooLong,
    NameInvalidCharacters,
    NameHyphenAtEdge,
    NameDoubleHyphen,

    /// <summary>A folder with this name is already in the skills folder.</summary>
    NameTaken,
    DescriptionEmpty,
    DescriptionTooLong,
}
