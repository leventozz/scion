namespace Scion.Application.Skills.Copying;

internal static class SkillFolderComparerExtensions
{
    /// <summary>A folder that cannot be compared is treated as different: the safe side is to ask the user.</summary>
    public static async Task<bool> AreIdenticalOrDifferentAsync(
        this ISkillFolderComparer comparer,
        AgentLocation source,
        string folderName,
        AgentLocation target,
        CancellationToken cancellationToken)
    {
        try
        {
            return await comparer.AreIdenticalAsync(source, folderName, target, cancellationToken);
        }
        catch (SkillCopyFailedException)
        {
            return false;
        }
    }
}
