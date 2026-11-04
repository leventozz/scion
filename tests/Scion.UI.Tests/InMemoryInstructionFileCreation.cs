using System.Text;
using Scion.Application.Creating;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>Creates an instruction file where the viewing fake can see it, and refuses to replace one.</summary>
internal sealed class InMemoryInstructionFileCreation(InMemoryInstructionFiles instructionFiles) : IInstructionFileCreator
{
    private InstructionFileCreateFailure? _failure;

    public void FailWith(InstructionFileCreateFailure? failure) => _failure = failure;

    public Task<string> CreateAsync(Location location, Agent agent, string content, CancellationToken cancellationToken)
    {
        if (_failure is { } failure)
        {
            throw new InstructionFileCreateFailedException(failure);
        }

        var relativePath = (location, agent) switch
        {
            (Location.Personal, Agent.ClaudeCode) => ".claude/CLAUDE.md",
            (Location.Personal, _) => ".codex/AGENTS.md",
            (_, Agent.ClaudeCode) => "CLAUDE.md",
            _ => "AGENTS.md",
        };

        if (instructionFiles.Has(location, agent, relativePath))
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.AlreadyExists);
        }

        instructionFiles.Set(location, agent, relativePath, Encoding.UTF8.GetBytes(content));
        return Task.FromResult(relativePath);
    }
}
