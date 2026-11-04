namespace Scion.Application.Editing;

public enum FileSaveFailure
{
    AccessDenied,
    FolderMissing,
    Other,

    /// <summary>The file no longer has the content the save was based on.</summary>
    ChangedOnDisk,

    /// <summary>The file is gone.</summary>
    DeletedOnDisk,
}
