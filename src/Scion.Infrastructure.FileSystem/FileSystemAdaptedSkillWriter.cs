using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Copies a skill folder like <see cref="FileSystemSkillCopier"/>, but its SKILL.md is the given bytes. Every other file
/// (scripts, references, assets) is copied byte for byte, so only the SKILL.md is ever adapted.
/// </summary>
public sealed class FileSystemAdaptedSkillWriter : IAdaptedSkillWriter
{
    private const string SkillFileName = "SKILL.md";

    private readonly SkillFolderTransfer _transfer;

    public FileSystemAdaptedSkillWriter(string homeDirectory, IEnumerable<IAgentLayout> layouts)
        : this(homeDirectory, layouts, Directory.Move)
    {
    }

    /// <param name="moveDirectory">The one step that tests replace to make a move fail.</param>
    internal FileSystemAdaptedSkillWriter(
        string homeDirectory, IEnumerable<IAgentLayout> layouts, Action<string, string> moveDirectory) =>
        _transfer = new SkillFolderTransfer(new SkillsDirectories(homeDirectory, layouts), moveDirectory);

    public Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken) =>
        SkillFolderTransfer.RunAsync(
            folderName, () => _transfer.Copy(source, folderName, target, FillWith(skillFile), cancellationToken));

    public Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken) =>
        SkillFolderTransfer.RunAsync(
            folderName, () => _transfer.Replace(source, folderName, target, FillWith(skillFile), cancellationToken));

    private static SkillFolderTransfer.Fill FillWith(byte[] skillFile) => (sourceDirectory, stagingDirectory, cancellationToken) =>
    {
        SkillFolderTransfer.CopyTree(sourceDirectory, stagingDirectory, cancellationToken);
        File.WriteAllBytes(Path.Combine(stagingDirectory, SkillFileName), skillFile);
    };
}
