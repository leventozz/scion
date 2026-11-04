namespace Scion.Application.Editing;

/// <summary>A backup adapter failure; a save goes on without the backup.</summary>
public sealed class FileBackupFailedException(Exception? innerException = null)
    : Exception("The previous version of the file could not be backed up.", innerException);
