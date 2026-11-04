using Scion.Domain.Skills;

namespace Scion.Application.Skills;

/// <summary>The rules that turn the raw data of a skill folder into what the user sees, warnings included.</summary>
internal static class SkillValidation
{
    public static SkillSummary Summarize(SkillFolder folder)
    {
        if (folder.FrontmatterUnreadable)
        {
            return new SkillSummary(folder.FolderName, folder.FolderName, "", [SkillWarning.FrontmatterUnreadable], !folder.IsLinked);
        }

        var description = folder.Description ?? "";
        var warnings = WarningsForName(folder).Concat(WarningsForDescription(description)).ToList();

        return new SkillSummary(DisplayNameOf(folder), folder.FolderName, description, warnings, !folder.IsLinked);
    }

    private static string DisplayNameOf(SkillFolder folder) =>
        string.IsNullOrEmpty(folder.Name) ? folder.FolderName : folder.Name;

    private static IEnumerable<SkillWarning> WarningsForName(SkillFolder folder)
    {
        var result = SkillName.Create(folder.Name ?? "");

        if (!result.IsSuccess)
        {
            yield return SkillWarnings.From(result.Error);
        }
        else if (result.Value.Value != folder.FolderName)
        {
            yield return SkillWarning.NameDoesNotMatchFolder;
        }
    }

    private static IEnumerable<SkillWarning> WarningsForDescription(string description)
    {
        var result = SkillDescription.Create(description);

        if (!result.IsSuccess)
        {
            yield return SkillWarnings.From(result.Error);
        }
    }
}
