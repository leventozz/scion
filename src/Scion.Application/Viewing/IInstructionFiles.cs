using Scion.Domain;

namespace Scion.Application.Viewing;

public interface IInstructionFiles
{
    /// <summary>
    /// The instruction files that exist in a location for an agent, in the order of the agent's convention. Each
    /// file is read up to <paramref name="maxBytes"/> + 1 bytes; one that cannot be read has a failed <c>Read</c>.
    /// </summary>
    Task<IReadOnlyList<InstructionFileBytes>> ReadAsync(
        Location location, Agent agent, int maxBytes, CancellationToken cancellationToken);
}
