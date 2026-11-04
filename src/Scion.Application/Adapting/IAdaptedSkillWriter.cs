using Scion.Application.Skills.Copying;

namespace Scion.Application.Adapting;

/// <summary>
/// Puts a skill into the target the way <see cref="ISkillFolderCopier"/> does (the whole folder, scripts and references
/// byte for byte), except that its SKILL.md is the given bytes. Same contract, same <see cref="SkillCopyFailedException"/>.
/// </summary>
public interface IAdaptedSkillWriter
{
    /// <summary>Throws <see cref="SkillCopyFailedException"/> if the target already has the skill folder.</summary>
    Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken);

    /// <summary>On failure the previous folder of the target stays intact and <see cref="SkillCopyFailedException"/> is thrown.</summary>
    Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken);
}
