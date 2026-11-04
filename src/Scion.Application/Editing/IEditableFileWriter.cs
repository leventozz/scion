namespace Scion.Application.Editing;

public interface IEditableFileWriter
{
    /// <summary>
    /// Replaces the content of the file in one step: afterwards the file holds all of <paramref name="content"/> or, when
    /// this throws, what it held before. Throws <see cref="FileSaveFailedException"/>.
    /// </summary>
    /// <param name="unchangedSinceHash">
    /// The <see cref="ContentHash"/> the file on disk must still have. The adapter checks it as late as it can, right
    /// before it replaces the file, and throws <see cref="FileSaveFailure.ChangedOnDisk"/> or
    /// <see cref="FileSaveFailure.DeletedOnDisk"/> instead of replacing it. Null replaces the file whatever it holds.
    /// </param>
    Task WriteAsync(
        EditableFile file, ReadOnlyMemory<byte> content, string? unchangedSinceHash, CancellationToken cancellationToken);
}
