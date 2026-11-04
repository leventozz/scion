using Scion.Application.Editing;

namespace Scion.UI;

/// <summary>The words of editing: what the user reads when opening or saving a file goes wrong.</summary>
internal static class EditMessages
{
    public const string BackupSkipped = "Saved, but the previous version couldn't be backed up.";

    public static string For(OpenForEditingError error) => error switch
    {
        OpenForEditingError.NotEditable => "This file can't be edited here.",
        _ => "Couldn't read the file on disk.",
    };

    /// <summary>Only for the errors a save reports as a message; a changed or deleted file asks a question instead.</summary>
    public static string For(SaveFileError error) => error switch
    {
        SaveFileError.Unreadable => "Couldn't read the file on disk.",
        SaveFileError.WriteFailed { Failure: FileSaveFailure.AccessDenied } => "Couldn't save: access to the file was denied.",
        SaveFileError.WriteFailed { Failure: FileSaveFailure.FolderMissing } => "Couldn't save: the folder no longer exists.",
        _ => "Couldn't save the file.",
    };
}
