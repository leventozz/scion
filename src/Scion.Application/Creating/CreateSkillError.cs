namespace Scion.Application.Creating;

public enum CreateSkillError
{
    /// <summary>Name or description break the rules; the dialog normally stops this before it gets here.</summary>
    Invalid,

    /// <summary>A skill folder of that name is already there. Nothing was changed.</summary>
    AlreadyExists,
    FolderMissing,
    WriteFailed,
}
