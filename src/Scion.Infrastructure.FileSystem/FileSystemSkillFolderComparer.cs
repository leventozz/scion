using System.Security.Cryptography;
using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

/// <summary>Two skill folders are identical when they hold the same relative file paths with the same SHA-256 hashes.</summary>
public sealed class FileSystemSkillFolderComparer : ISkillFolderComparer
{
    private readonly SkillsDirectories _skillsDirectories;

    public FileSystemSkillFolderComparer(string homeDirectory, IEnumerable<IAgentLayout> layouts) =>
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);

    public Task<bool> AreIdenticalAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        SkillFolderName.EnsureIsSingleSegment(folderName);

        return Task.Run(() => AreIdentical(FolderOf(source, folderName), FolderOf(target, folderName), folderName, cancellationToken));
    }

    private string FolderOf(AgentLocation place, string folderName) =>
        Path.Combine(_skillsDirectories.For(place.Location, place.Agent), folderName);

    private static bool AreIdentical(string first, string second, string folderName, CancellationToken cancellationToken)
    {
        try
        {
            var firstHashes = HashesOf(first, folderName, cancellationToken);
            var secondHashes = HashesOf(second, folderName, cancellationToken);

            return firstHashes.Count == secondHashes.Count
                && firstHashes.All(entry => secondHashes.TryGetValue(entry.Key, out var hash) && hash.SequenceEqual(entry.Value));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SkillCopyFailedException(folderName, exception);
        }
    }

    private static Dictionary<string, byte[]> HashesOf(string folder, string folderName, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            throw new SkillCopyFailedException(folderName);
        }

        var hashes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(file);
            hashes[Path.GetRelativePath(folder, file).Replace('\\', '/')] = SHA256.HashData(stream);
        }

        return hashes;
    }
}
