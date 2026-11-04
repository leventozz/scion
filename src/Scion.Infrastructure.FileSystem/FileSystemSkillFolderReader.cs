using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemSkillFolderReader : ISkillFolderReader
{
    private const string SkillFileName = "SKILL.md";

    private readonly SkillsDirectories _skillsDirectories;

    public FileSystemSkillFolderReader(string homeDirectory, IEnumerable<IAgentLayout> layouts) =>
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);

    public async Task<IReadOnlyList<SkillFolder>> ReadAsync(
        Location location, Agent agent, CancellationToken cancellationToken)
    {
        var skillsDirectory = _skillsDirectories.For(location, agent);
        if (!Directory.Exists(skillsDirectory))
        {
            return [];
        }

        var inLinkedArea = FolderLinks.IsInLinkedSkillsArea(skillsDirectory);
        var skillFolders = new List<SkillFolder>();
        foreach (var directory in Directory.EnumerateDirectories(skillsDirectory))
        {
            // Folders that start with a dot are never skills (Scion's own scratch folders, tool folders).
            if (Path.GetFileName(directory).StartsWith('.') || SkillFileIn(directory) is not { } skillFile)
            {
                continue;
            }

            var frontmatter = await SkillFrontmatterReader.ReadAsync(skillFile, cancellationToken);
            skillFolders.Add(new SkillFolder(
                Path.GetFileName(directory),
                frontmatter.Name,
                frontmatter.Description,
                frontmatter.IsUnreadable,
                IsLinked: inLinkedArea || FolderLinks.IsLink(directory)));
        }

        return skillFolders;
    }

    // File.Exists is case-insensitive on Windows, so compare the listed names ourselves.
    private static string? SkillFileIn(string directory) =>
        Directory.EnumerateFiles(directory)
            .FirstOrDefault(file => string.Equals(Path.GetFileName(file), SkillFileName, StringComparison.Ordinal));
}
