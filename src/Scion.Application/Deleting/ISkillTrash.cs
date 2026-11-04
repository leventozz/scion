using Scion.Application.Skills.Copying;

namespace Scion.Application.Deleting;

public interface ISkillTrash
{
    /// <summary>
    /// Moves the whole skill folder to Scion's trash and records where it came from. The skill is never destroyed:
    /// when moving fails, nothing has changed and <see cref="SkillTrashFailedException"/> says why.
    /// </summary>
    Task<TrashedSkill> MoveToTrashAsync(
        AgentLocation place, string folderName, DateTimeOffset now, CancellationToken cancellationToken);
}
