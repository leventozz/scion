using Scion.Application.Skills.Copying;

namespace Scion.Application.Adapting;

public interface ITargetContextReader
{
    /// <summary>
    /// Reads the allowlisted context of the target: the agent's instruction files, project manifests and the names of the
    /// folders two levels deep. Never the content of a source file. A file is read for at most
    /// <c>limits.MaxFileBytes + 1</c> bytes.
    /// </summary>
    Task<TargetContext> ReadAsync(AgentLocation target, ContextLimits limits, CancellationToken cancellationToken);
}
