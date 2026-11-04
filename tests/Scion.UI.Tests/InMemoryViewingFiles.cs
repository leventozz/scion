using System.Text;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.UI.Tests;

internal sealed class InMemorySkillFiles : ISkillFiles
{
    private readonly Dictionary<(Location, Agent, string), Folder> _folders = new();

    public void Add(Location location, Agent agent, string folderName, string folderPath, params (string Path, string Content)[] files) =>
        Add(location, agent, folderName, folderPath, files.Select(file => (file.Path, Encoding.UTF8.GetBytes(file.Content))).ToArray());

    public void Add(Location location, Agent agent, string folderName, string folderPath, params (string Path, byte[] Content)[] files) =>
        _folders[(location, agent, folderName)] = new Folder(folderPath, files.ToDictionary(file => file.Path, file => file.Content));

    public void AddBinary(Location location, Agent agent, string folderName, string relativePath, byte[] content) =>
        _folders[(location, agent, folderName)].Files[relativePath] = content;

    public bool HasFolder(Location location, Agent agent, string folderName) => _folders.ContainsKey((location, agent, folderName));

    public byte[]? Get(Location location, Agent agent, string folderName, string relativePath) =>
        _folders.TryGetValue((location, agent, folderName), out var folder) && folder.Files.TryGetValue(relativePath, out var content)
            ? content
            : null;

    public void Set(Location location, Agent agent, string folderName, string relativePath, byte[] content) =>
        _folders[(location, agent, folderName)].Files[relativePath] = content;

    public void Remove(Location location, Agent agent, string folderName, string relativePath) =>
        _folders[(location, agent, folderName)].Files.Remove(relativePath);

    public void MakeUnlistable(Location location, Agent agent, string folderName) =>
        _folders[(location, agent, folderName)] = new Folder("", [], Unlistable: true);

    public Task<SkillFolderListing?> ListAsync(Location location, Agent agent, string folderName, CancellationToken cancellationToken)
    {
        if (!_folders.TryGetValue((location, agent, folderName), out var folder))
        {
            return Task.FromResult<SkillFolderListing?>(null);
        }

        return folder.Unlistable
            ? throw new FileViewFailedException()
            : Task.FromResult<SkillFolderListing?>(new SkillFolderListing(folder.Path, [.. folder.Files.Keys]));
    }

    public Task<SkillFileContent> ReadAsync(
        Location location, Agent agent, string folderName, string relativePath, int maxBytes, CancellationToken cancellationToken)
    {
        var folder = _folders[(location, agent, folderName)];
        var directory = relativePath.Contains('/') ? "/" + relativePath[..relativePath.LastIndexOf('/')] : "";
        var bytes = folder.Files.TryGetValue(relativePath, out var content)
            ? Result<FileBytes, FileReadError>.Success(new FileBytes(content.Take(maxBytes + 1).ToArray(), content.Length))
            : Result<FileBytes, FileReadError>.Failure(FileReadError.Unreadable);

        return Task.FromResult(new SkillFileContent(folder.Path + directory, bytes));
    }

    private sealed record Folder(string Path, Dictionary<string, byte[]> Files, bool Unlistable = false);
}

internal sealed class InMemoryInstructionFiles : IInstructionFiles
{
    private readonly Dictionary<(Location, Agent), IReadOnlyList<InstructionFileBytes>> _files = new();

    public void Add(Location location, Agent agent, params InstructionFileBytes[] files) => _files[(location, agent)] = files;

    public bool Has(Location location, Agent agent, string relativePath) =>
        _files.TryGetValue((location, agent), out var files) && files.Any(file => file.RelativePath == relativePath);

    public byte[]? Get(Location location, Agent agent, string relativePath) =>
        _files.TryGetValue((location, agent), out var files)
            ? files.FirstOrDefault(file => file.RelativePath == relativePath)?.Read.Value.Head
            : null;

    public void Set(Location location, Agent agent, string relativePath, byte[] content)
    {
        var files = _files.TryGetValue((location, agent), out var existing) ? existing.ToList() : [];
        var index = files.FindIndex(file => file.RelativePath == relativePath);
        var folderPath = index >= 0 ? files[index].FolderPath : "/";
        var updated = new InstructionFileBytes(
            relativePath, folderPath, Result<FileBytes, FileReadError>.Success(new FileBytes(content, content.Length)));

        if (index >= 0)
        {
            files[index] = updated;
        }
        else
        {
            files.Add(updated);
        }

        _files[(location, agent)] = files;
    }

    public void Remove(Location location, Agent agent, string relativePath) =>
        _files[(location, agent)] = _files[(location, agent)].Where(file => file.RelativePath != relativePath).ToList();

    public static InstructionFileBytes File(string relativePath, string folderPath, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        return new InstructionFileBytes(relativePath, folderPath, Result<FileBytes, FileReadError>.Success(new FileBytes(bytes, bytes.Length)));
    }

    public static InstructionFileBytes Binary(string relativePath, string folderPath) =>
        new(relativePath, folderPath, Result<FileBytes, FileReadError>.Success(new FileBytes([1, 0, 2], 3)));

    public Task<IReadOnlyList<InstructionFileBytes>> ReadAsync(
        Location location, Agent agent, int maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(_files.TryGetValue((location, agent), out var files) ? files : []);
}
