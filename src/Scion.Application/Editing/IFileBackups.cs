namespace Scion.Application.Editing;

public interface IFileBackups
{
    /// <summary>
    /// Keeps a copy of the file as it is on disk now. A file that does not exist is a no-op. Throws
    /// <see cref="FileBackupFailedException"/>.
    /// </summary>
    Task KeepPreviousVersionAsync(EditableFile file, DateTimeOffset now, CancellationToken cancellationToken);
}
