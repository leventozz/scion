namespace Scion.Application.Creating;

public enum CreateInstructionFileError
{
    /// <summary>An instruction file is already there. Nothing was changed.</summary>
    AlreadyExists,
    FolderMissing,
    WriteFailed,
}
