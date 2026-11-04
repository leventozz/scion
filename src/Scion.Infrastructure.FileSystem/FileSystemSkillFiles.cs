using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemSkillFiles : ISkillFiles
{
    private readonly SkillsDirectories _skillsDirectories;

    public FileSystemSkillFiles(string homeDirectory, IEnumerable<IAgentLayout> layouts) =>
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);

    public Task<SkillFolderListing?> ListAsync(
        Location location, Agent agent, string folderName, CancellationToken cancellationToken)
    {
        var folder = FolderOf(location, agent, folderName);
        if (!Directory.Exists(folder))
        {
            return Task.FromResult<SkillFolderListing?>(null);
        }

        try
        {
            return Task.FromResult<SkillFolderListing?>(new SkillFolderListing(folder, [.. FilesUnder(folder, prefix: "")]));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileViewFailedException(exception);
        }
    }

    public async Task<SkillFileContent> ReadAsync(
        Location location, Agent agent, string folderName, string relativePath, int maxBytes, CancellationToken cancellationToken)
    {
        var folder = FolderOf(location, agent, folderName);
        var filePath = Path.GetFullPath(Path.Combine(folder, relativePath));
        EnsureInside(folder, filePath);

        return new SkillFileContent(Path.GetDirectoryName(filePath)!, await FileHead.ReadAsync(filePath, maxBytes, cancellationToken));
    }

    private string FolderOf(Location location, Agent agent, string folderName)
    {
        SkillFolderName.EnsureIsSingleSegment(folderName, _ => new FileViewFailedException());

        return Path.Combine(_skillsDirectories.For(location, agent), folderName);
    }

    // The Application layer already refuses such paths; this keeps a bug there from reading outside the skill folder.
    private static void EnsureInside(string folder, string filePath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), filePath);
        var leavesFolder = relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative);

        if (leavesFolder)
        {
            throw new FileViewFailedException();
        }
    }

    private static IEnumerable<string> FilesUnder(string directory, string prefix)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (!name.StartsWith('.'))
            {
                yield return prefix + name;
            }
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDirectory);
            if (name.StartsWith('.'))
            {
                continue;
            }

            foreach (var file in FilesUnder(subDirectory, $"{prefix}{name}/"))
            {
                yield return file;
            }
        }
    }
}
