using Scion.Domain;

namespace Scion.Application.Creating;

public interface IInstructionFileCreator
{
    /// <summary>
    /// Creates the instruction file the agent reads first in the location and returns its path relative to the
    /// location root. Never replaces a file: if one is there, nothing is touched and
    /// <see cref="InstructionFileCreateFailedException"/> carries <see cref="InstructionFileCreateFailure.AlreadyExists"/>.
    /// </summary>
    Task<string> CreateAsync(Location location, Agent agent, string content, CancellationToken cancellationToken);
}
