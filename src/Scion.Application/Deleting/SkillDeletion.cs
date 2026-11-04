namespace Scion.Application.Deleting;

public enum SkillDeletion
{
    MovedToTrash,

    /// <summary>The skill is in the trash, but some files are still in the skill folder.</summary>
    OriginalNotFullyRemoved,
}
