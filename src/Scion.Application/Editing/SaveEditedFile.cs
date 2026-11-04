using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Editing;

public sealed class SaveEditedFile(
    IEditableFileReader reader,
    IEditableFileWriter writer,
    IFileBackups backups,
    TimeProvider timeProvider)
{
    public async Task<Result<SavedFile, SaveFileError>> ExecuteAsync(
        EditableFile file, string text, EditBaseline baseline, SaveMode mode, CancellationToken cancellationToken = default)
    {
        if (!EditablePaths.IsSafe(file))
        {
            return Failure(new SaveFileError.NotInSkillFolder());
        }

        var current = await reader.ReadAsync(file, FileText.MaxBytes, cancellationToken);
        var isMissing = !current.IsSuccess && current.Error == FileReadError.Missing;

        if (mode == SaveMode.CheckForChanges && CheckForChanges(current, baseline) is { } conflict)
        {
            return Failure(conflict);
        }

        var backupSkipped = !isMissing && !await TryKeepPreviousVersionAsync(file, cancellationToken);

        var content = baseline.Format.Encode(text);
        try
        {
            await writer.WriteAsync(
                file, content, mode == SaveMode.CheckForChanges ? baseline.Hash : null, cancellationToken);
        }
        catch (FileSaveFailedException exception)
        {
            return Failure(ErrorOf(exception.Failure));
        }

        var newBaseline = baseline with { Hash = ContentHash.Of(content) };

        return Result<SavedFile, SaveFileError>.Success(new SavedFile(newBaseline, backupSkipped));
    }

    private static SaveFileError ErrorOf(FileSaveFailure failure) => failure switch
    {
        FileSaveFailure.ChangedOnDisk => new SaveFileError.ChangedOnDisk(),
        FileSaveFailure.DeletedOnDisk => new SaveFileError.DeletedOnDisk(),
        _ => new SaveFileError.WriteFailed(failure),
    };

    private static SaveFileError? CheckForChanges(Result<FileBytes, FileReadError> current, EditBaseline baseline)
    {
        if (!current.IsSuccess)
        {
            return current.Error == FileReadError.Missing
                ? new SaveFileError.DeletedOnDisk()
                : new SaveFileError.Unreadable();
        }

        var isUnchanged = current.Value.Length <= FileText.MaxBytes && ContentHash.Of(current.Value.Head) == baseline.Hash;

        return isUnchanged ? null : new SaveFileError.ChangedOnDisk();
    }

    private async Task<bool> TryKeepPreviousVersionAsync(EditableFile file, CancellationToken cancellationToken)
    {
        try
        {
            await backups.KeepPreviousVersionAsync(file, timeProvider.GetUtcNow(), cancellationToken);
            return true;
        }
        catch (FileBackupFailedException)
        {
            return false;
        }
    }

    private static Result<SavedFile, SaveFileError> Failure(SaveFileError error) =>
        Result<SavedFile, SaveFileError>.Failure(error);
}
