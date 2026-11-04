using System.Text;
using Scion.Application.Editing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>
/// The editing ports over the same in-memory files the viewing fakes serve, so a saved file is what the next view reads.
/// </summary>
internal sealed class InMemoryEditableFiles(InMemorySkillFiles skillFiles, InMemoryInstructionFiles instructionFiles)
    : IEditableFileReader, IEditableFileWriter
{
    private readonly HashSet<EditableFile> _unreadable = [];
    private FileSaveFailure? _writeFailure;
    private TaskCompletionSource? _writeGate;

    public List<EditableFile> Written { get; } = [];

    public void FailWritesWith(FileSaveFailure? failure) => _writeFailure = failure;

    public void MakeUnreadable(EditableFile file) => _unreadable.Add(file);

    /// <summary>Writes wait until the returned source is completed, so a test can look at the page while it saves.</summary>
    public TaskCompletionSource HoldWrites() => _writeGate = new TaskCompletionSource();

    public void ChangeBehindTheScenes(EditableFile file, string text) => Put(file, Encoding.UTF8.GetBytes(text));

    public void Delete(EditableFile file)
    {
        switch (file)
        {
            case EditableFile.SkillFile skill:
                skillFiles.Remove(skill.Location, skill.Agent, skill.FolderName, skill.RelativePath);
                break;
            case EditableFile.InstructionFile instruction:
                instructionFiles.Remove(instruction.Location, instruction.Agent, instruction.RelativePath);
                break;
        }
    }

    public byte[]? BytesOf(EditableFile file) => file switch
    {
        EditableFile.SkillFile skill => skillFiles.Get(skill.Location, skill.Agent, skill.FolderName, skill.RelativePath),
        EditableFile.InstructionFile instruction => instructionFiles.Get(instruction.Location, instruction.Agent, instruction.RelativePath),
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, null),
    };

    public string? TextOf(EditableFile file) => BytesOf(file) is { } bytes ? Encoding.UTF8.GetString(bytes) : null;

    public Task<Result<FileBytes, FileReadError>> ReadAsync(
        EditableFile file, int maxBytes, CancellationToken cancellationToken)
    {
        if (BytesOf(file) is not { } content)
        {
            return Task.FromResult(Result<FileBytes, FileReadError>.Failure(FileReadError.Missing));
        }

        return Task.FromResult(_unreadable.Contains(file)
            ? Result<FileBytes, FileReadError>.Failure(FileReadError.Unreadable)
            : Result<FileBytes, FileReadError>.Success(new FileBytes(content.Take(maxBytes + 1).ToArray(), content.Length)));
    }

    public async Task WriteAsync(
        EditableFile file, ReadOnlyMemory<byte> content, string? unchangedSinceHash, CancellationToken cancellationToken)
    {
        if (_writeGate is { } gate)
        {
            await gate.Task;
        }

        if (_writeFailure is { } failure)
        {
            throw new FileSaveFailedException(failure);
        }

        Put(file, content.ToArray());
        Written.Add(file);
    }

    private void Put(EditableFile file, byte[] content)
    {
        switch (file)
        {
            case EditableFile.SkillFile skill:
                skillFiles.Set(skill.Location, skill.Agent, skill.FolderName, skill.RelativePath, content);
                break;
            case EditableFile.InstructionFile instruction:
                instructionFiles.Set(instruction.Location, instruction.Agent, instruction.RelativePath, content);
                break;
        }
    }
}
