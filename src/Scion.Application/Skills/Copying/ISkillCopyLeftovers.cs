namespace Scion.Application.Skills.Copying;

public interface ISkillCopyLeftovers
{
    /// <summary>
    /// Clears what an interrupted copy left in the scratch directory of this agent in this location. Best effort:
    /// it never throws, and it puts an old skill folder back when that is the only copy of the skill.
    /// </summary>
    Task CleanUpAsync(AgentLocation place, CancellationToken cancellationToken);
}
