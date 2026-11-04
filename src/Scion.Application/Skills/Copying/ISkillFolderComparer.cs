namespace Scion.Application.Skills.Copying;

public interface ISkillFolderComparer
{
    /// <summary>
    /// Whether the skill folder has exactly the same files with exactly the same content on both sides.
    /// Throws <see cref="SkillCopyFailedException"/> if either folder cannot be read.
    /// </summary>
    Task<bool> AreIdenticalAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken);
}
