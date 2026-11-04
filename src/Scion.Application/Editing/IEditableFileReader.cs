using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Editing;

public interface IEditableFileReader
{
    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> + 1 bytes of the file as it is on disk now. A file that does not exist
    /// is <see cref="FileReadError.Missing"/>; one that is locked or not permitted is <see cref="FileReadError.Unreadable"/>.
    /// </summary>
    Task<Result<FileBytes, FileReadError>> ReadAsync(EditableFile file, int maxBytes, CancellationToken cancellationToken);
}
