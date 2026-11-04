namespace Scion.Application.Skills.Copying;

public interface ISkillFolderCopier
{
    /// <summary>Copies the whole skill folder. Throws <see cref="SkillCopyFailedException"/> if the target already has it.</summary>
    Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the skill folder in the target with the source's. On failure the previous folder stays intact
    /// and <see cref="SkillCopyFailedException"/> is thrown.
    /// </summary>
    Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken);
}
