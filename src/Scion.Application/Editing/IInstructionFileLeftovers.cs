using Scion.Domain;

namespace Scion.Application.Editing;

public interface IInstructionFileLeftovers
{
    /// <summary>
    /// Removes the temp files an interrupted save of an instruction file (CLAUDE.md, AGENTS.md) left beside it in this
    /// location. Best effort: it never throws and never touches anything else.
    /// </summary>
    Task CleanUpAsync(Location location, CancellationToken cancellationToken);
}
