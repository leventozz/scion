using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>Where an agent keeps its instruction file; separate from <see cref="IAgentLayout"/> (ISP).</summary>
public interface IInstructionFileLayout
{
    Agent Agent { get; }

    /// <summary>Relative to the project root, <c>/</c>-separated, in the order the agent reads them.</summary>
    IReadOnlyList<string> ProjectInstructionFiles { get; }

    /// <summary>Relative to the personal root (the home directory), <c>/</c>-separated.</summary>
    IReadOnlyList<string> PersonalInstructionFiles { get; }
}
