using System.Text;
using Scion.Application.Editing;
using Scion.Application.Tests.Viewing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Editing;

/// <summary>Both editing ports over a dictionary of file contents.</summary>
internal sealed class InMemoryEditableFiles(List<string>? log = null) : IEditableFileReader, IEditableFileWriter
{
    private readonly Dictionary<EditableFile, byte[]> _files = new();
    private readonly HashSet<EditableFile> _unreadable = [];
    private FileSaveFailure? _writeFailure;

    public int ReadCalls { get; private set; }

    public int? LastMaxBytes { get; private set; }

    public int WriteCalls { get; private set; }

    /// <summary>The hash each write was asked to verify (null: no verification).</summary>
    public List<string?> VerifiedHashes { get; } = [];

    public void Put(EditableFile file, byte[] content) => _files[file] = content;

    public void Put(EditableFile file, string content) => Put(file, Encoding.UTF8.GetBytes(content));

    public void Delete(EditableFile file) => _files.Remove(file);

    public void MakeUnreadable(EditableFile file) => _unreadable.Add(file);

    public void FailWritesWith(FileSaveFailure failure) => _writeFailure = failure;

    public byte[] ContentOf(EditableFile file) => _files[file];

    public string TextOf(EditableFile file) => Encoding.UTF8.GetString(_files[file]);

    public Task<Result<FileBytes, FileReadError>> ReadAsync(
        EditableFile file, int maxBytes, CancellationToken cancellationToken)
    {
        ReadCalls++;
        LastMaxBytes = maxBytes;

        if (!_files.TryGetValue(file, out var content))
        {
            return Task.FromResult(Result<FileBytes, FileReadError>.Failure(FileReadError.Missing));
        }

        return Task.FromResult(
            _unreadable.Contains(file) ? Bytes.Unreadable : Bytes.Read(content, maxBytes));
    }

    public Task WriteAsync(
        EditableFile file, ReadOnlyMemory<byte> content, string? unchangedSinceHash, CancellationToken cancellationToken)
    {
        WriteCalls++;
        VerifiedHashes.Add(unchangedSinceHash);
        log?.Add("write");

        if (_writeFailure is { } failure)
        {
            throw new FileSaveFailedException(failure);
        }

        _files[file] = content.ToArray();
        return Task.CompletedTask;
    }
}
