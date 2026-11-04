namespace Scion.Application.Deleting;

public enum SkillTrashFailure
{
    /// <summary>The skill folder is not there (any more).</summary>
    Missing,

    /// <summary>The skill folder, its skills directory or its agent folder is a link. Nothing was touched.</summary>
    Linked,
    Other,
}
