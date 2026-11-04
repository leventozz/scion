using System.Text;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

internal sealed class InMemorySkillFiles : ISkillFiles
{
    private readonly Dictionary<(Location, Agent, string), Folder> _folders = new();

    public int ListCalls { get; private set; }

    public int ReadCalls { get; private set; }

    public int? LastMaxBytes { get; private set; }

    public void Add(Location location, Agent agent, string folderName, string folderPath, params (string Path, byte[] Content)[] files) =>
        _folders[(location, agent, folderName)] = new Folder(folderPath, files.ToDictionary(file => file.Path, file => file.Content));

    public void Add(Location location, Agent agent, string folderName, string folderPath, params (string Path, string Content)[] files) =>
        Add(location, agent, folderName, folderPath, files.Select(file => (file.Path, Encoding.UTF8.GetBytes(file.Content))).ToArray());

    public void MakeUnlistable(Location location, Agent agent, string folderName) =>
        _folders[(location, agent, folderName)] = new Folder("", [], Unlistable: true);

    public void MakeUnreadable(Location location, Agent agent, string folderName, string path) =>
        _folders[(location, agent, folderName)].Unreadable.Add(path);

    public Task<SkillFolderListing?> ListAsync(Location location, Agent agent, string folderName, CancellationToken cancellationToken)
    {
        ListCalls++;
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
        ReadCalls++;
        LastMaxBytes = maxBytes;
        var folder = _folders[(location, agent, folderName)];
        var directory = relativePath.Contains('/') ? "/" + relativePath[..relativePath.LastIndexOf('/')] : "";
        var bytes = folder.Files.TryGetValue(relativePath, out var content) && !folder.Unreadable.Contains(relativePath)
            ? Bytes.Read(content, maxBytes)
            : Bytes.Unreadable;

        return Task.FromResult(new SkillFileContent(folder.Path + directory, bytes));
    }

    private sealed record Folder(string Path, Dictionary<string, byte[]> Files, bool Unlistable = false)
    {
        public HashSet<string> Unreadable { get; } = [];
    }
}
