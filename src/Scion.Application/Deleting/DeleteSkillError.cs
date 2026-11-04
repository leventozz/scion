namespace Scion.Application.Deleting;

public enum DeleteSkillError
{
    SkillMissing,

    /// <summary>The skill is shared through a link; deleting it would touch what the link points to.</summary>
    Linked,
    Failed,
}
