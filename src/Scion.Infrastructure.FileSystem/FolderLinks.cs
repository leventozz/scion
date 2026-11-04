namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Symbolic links and junctions. Only those count as links: other reparse points (a OneDrive folder, say) are
/// ordinary folders for Scion.
/// </summary>
internal static class FolderLinks
{
    public static bool IsLink(string directory) => new DirectoryInfo(directory).LinkTarget is not null;

    /// <summary>
    /// The skills directory or its agent folder (".claude", ".agents") is a link. Everything in it then lives
    /// somewhere else and may be shared with other places.
    /// </summary>
    public static bool IsInLinkedSkillsArea(string skillsDirectory) =>
        IsLink(skillsDirectory) || IsLink(Path.GetDirectoryName(skillsDirectory)!);
}
