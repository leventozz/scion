using Scion.Application.Editing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Reads and writes the files Scion edits. A write stages the new content in a temp file and moves it over the
/// target, so the file holds either all the old content or all the new content.
/// </summary>
public sealed class FileSystemEditableFiles : IEditableFileReader, IEditableFileWriter
{
    private readonly EditableFilePaths _paths;
    private readonly Action<string, string> _moveFile;

    public FileSystemEditableFiles(
        string homeDirectory, IEnumerable<IAgentLayout> layouts, IEnumerable<IInstructionFileLayout> instructionLayouts)
        : this(homeDirectory, layouts, instructionLayouts, (from, to) => File.Move(from, to, overwrite: true))
    {
    }

    /// <param name="moveFile">The one step that tests replace to make a move fail. It replaces an existing target.</param>
    internal FileSystemEditableFiles(
        string homeDirectory,
        IEnumerable<IAgentLayout> layouts,
        IEnumerable<IInstructionFileLayout> instructionLayouts,
        Action<string, string> moveFile)
    {
        _paths = new EditableFilePaths(homeDirectory, layouts, instructionLayouts);
        _moveFile = moveFile;
    }

    public Task<Result<FileBytes, FileReadError>> ReadAsync(
        EditableFile file, int maxBytes, CancellationToken cancellationToken) =>
        FileHead.ReadAsync(_paths.PathOf(file), maxBytes, cancellationToken);

    public async Task WriteAsync(
        EditableFile file, ReadOnlyMemory<byte> content, string? unchangedSinceHash, CancellationToken cancellationToken)
    {
        var target = _paths.PathOf(file);
        EnsureWritable(target);

        var scratch = file is EditableFile.SkillFile skillFile ? _paths.ScratchOf(skillFile) : null;
        var staging = scratch is null
            ? InstructionTempFile.NewPathBeside(target)
            : CopyScratch.NewEditStaging(scratch);

        try
        {
            if (scratch is not null)
            {
                Directory.CreateDirectory(scratch);
            }

            await File.WriteAllBytesAsync(staging, content, cancellationToken);
            await EnsureUnchangedAsync(target, unchangedSinceHash, cancellationToken);
            _moveFile(staging, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileSaveFailedException(FailureOf(exception), exception);
        }
        finally
        {
            BestEffort.DeleteFile(staging);
            if (scratch is not null)
            {
                BestEffort.DeleteDirectoryIfEmpty(scratch);
            }
        }
    }

    // A read-only file is the user's way of saying "do not write here"; a move would replace it on some systems.
    private static void EnsureWritable(string target)
    {
        if (!Directory.Exists(Path.GetDirectoryName(target)))
        {
            throw new FileSaveFailedException(FileSaveFailure.FolderMissing);
        }

        if (File.Exists(target) && File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly))
        {
            throw new FileSaveFailedException(FileSaveFailure.AccessDenied);
        }
    }

    // The last look before the move, so the gap in which an outside change goes unnoticed is as small as it can be.
    private static async Task EnsureUnchangedAsync(string target, string? unchangedSinceHash, CancellationToken cancellationToken)
    {
        if (unchangedSinceHash is null)
        {
            return;
        }

        string hash;
        try
        {
            await using var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            hash = await ContentHash.OfAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileSaveFailedException(FileSaveFailure.DeletedOnDisk, exception);
        }

        if (hash != unchangedSinceHash)
        {
            throw new FileSaveFailedException(FileSaveFailure.ChangedOnDisk);
        }
    }

    private static FileSaveFailure FailureOf(Exception exception) => exception switch
    {
        DirectoryNotFoundException => FileSaveFailure.FolderMissing,
        UnauthorizedAccessException => FileSaveFailure.AccessDenied,
        _ => FileSaveFailure.Other,
    };

}
