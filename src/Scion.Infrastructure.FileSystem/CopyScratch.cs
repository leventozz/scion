namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// The scratch directory of an agent folder (".claude/.scion-tmp"): where a copy stages new content and parks the
/// old folder while swapping, and where an edit stages the new content of a skill file. It lives beside the skills
/// directory, never inside it, so no skill reader sees it.
/// </summary>
internal static class CopyScratch
{
    public const string FolderName = ".scion-tmp";

    private const string StagingPrefix = "tmp-";
    private const string PreviousPrefix = "old-";
    private const string EditStagingPrefix = "edit-";
    private const int GuidSuffixLength = 33; // "-" + 32 hex digits

    public static string BesideSkills(string skillsDirectory) =>
        Path.Combine(Path.GetDirectoryName(skillsDirectory)!, FolderName);

    public static string NewStaging(string scratch, string skillName) => NewFolder(scratch, StagingPrefix, skillName);

    public static string NewPrevious(string scratch, string skillName) => NewFolder(scratch, PreviousPrefix, skillName);

    /// <summary>The path of a new staging file for an edited skill file ("edit-{guid}").</summary>
    public static string NewEditStaging(string scratch) => Path.Combine(scratch, $"{EditStagingPrefix}{Guid.NewGuid():N}");

    public static bool IsStaging(string folderName) => folderName.StartsWith(StagingPrefix, StringComparison.Ordinal);

    public static bool IsEditStaging(string fileName) => fileName.StartsWith(EditStagingPrefix, StringComparison.Ordinal);

    /// <summary>The skill an "old-{skill}-{guid}" folder was parked for.</summary>
    public static bool TryGetPreviousSkill(string folderName, out string skillName)
    {
        skillName = "";
        var isPrevious = folderName.StartsWith(PreviousPrefix, StringComparison.Ordinal)
            && folderName.Length > PreviousPrefix.Length + GuidSuffixLength
            && folderName[^GuidSuffixLength] == '-';

        if (isPrevious)
        {
            skillName = folderName[PreviousPrefix.Length..^GuidSuffixLength];
        }

        return isPrevious;
    }

    private static string NewFolder(string scratch, string prefix, string skillName) =>
        Path.Combine(scratch, $"{prefix}{skillName}-{Guid.NewGuid():N}");
}
