using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

/// <summary>Copies whole skill folders on disk (staged and moved into place by <see cref="SkillFolderTransfer"/>).</summary>
public sealed class FileSystemSkillCopier : ISkillFolderNames, ISkillFolderCopier
{
    private readonly SkillsDirectories _skillsDirectories;
    private readonly SkillFolderTransfer _transfer;

    public FileSystemSkillCopier(string homeDirectory, IEnumerable<IAgentLayout> layouts)
        : this(homeDirectory, layouts, Directory.Move)
    {
    }

    /// <param name="moveDirectory">The one step that tests replace to make a move fail.</param>
    internal FileSystemSkillCopier(
        string homeDirectory, IEnumerable<IAgentLayout> layouts, Action<string, string> moveDirectory)
    {
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);
        _transfer = new SkillFolderTransfer(_skillsDirectories, moveDirectory);
    }

    public Task<IReadOnlyList<string>> ListAsync(AgentLocation place, CancellationToken cancellationToken)
    {
        var skillsDirectory = _skillsDirectories.For(place.Location, place.Agent);
        IReadOnlyList<string> names = Directory.Exists(skillsDirectory)
            ? Directory.EnumerateDirectories(skillsDirectory).Select(directory => Path.GetFileName(directory)).ToList()
            : [];

        return Task.FromResult(names);
    }

    public Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken) =>
        SkillFolderTransfer.RunAsync(
            folderName, () => _transfer.Copy(source, folderName, target, SkillFolderTransfer.CopyTree, cancellationToken));

    public Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken) =>
        SkillFolderTransfer.RunAsync(
            folderName, () => _transfer.Replace(source, folderName, target, SkillFolderTransfer.CopyTree, cancellationToken));
}
