using Scion.Application.Skills.Copying;

namespace Scion.UI;

internal static class CopyMessages
{
    public static string For(CopySkillsError error) => error switch
    {
        CopySkillsError.SameSourceAndTarget => "Choose a different project or agent.",
        CopySkillsError.SourceFolderMissing => "The source folder no longer exists.",
        CopySkillsError.TargetFolderMissing => "The target folder no longer exists.",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
