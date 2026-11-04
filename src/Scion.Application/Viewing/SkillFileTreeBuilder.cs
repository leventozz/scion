namespace Scion.Application.Viewing;

internal static class SkillFileTreeBuilder
{
    private const string SkillFileName = "SKILL.md";

    /// <summary>
    /// Root SKILL.md, the other root files, then the folders with their children. Each group is sorted ignoring case;
    /// names stay exactly as they are on disk.
    /// </summary>
    public static IReadOnlyList<SkillFileNode> Build(IEnumerable<string> relativePaths) =>
        Level(relativePaths.Select(path => path.Split('/')), parentPath: "", isRoot: true);

    private static List<SkillFileNode> Level(IEnumerable<string[]> entries, string parentPath, bool isRoot)
    {
        var entryList = entries.ToList();

        var files = entryList
            .Where(entry => entry.Length == 1)
            .Select(entry => entry[0])
            .OrderBy(name => isRoot && name == SkillFileName ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (SkillFileNode)new SkillFileNode.File(name, parentPath + name));

        var folders = entryList
            .Where(entry => entry.Length > 1)
            .GroupBy(entry => entry[0])
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => (SkillFileNode)new SkillFileNode.Folder(
                group.Key, Level(group.Select(entry => entry[1..]), $"{parentPath}{group.Key}/", isRoot: false)));

        return [.. files, .. folders];
    }
}
