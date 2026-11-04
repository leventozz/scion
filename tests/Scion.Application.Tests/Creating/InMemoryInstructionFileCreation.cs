using Scion.Application.Creating;
using Scion.Domain;

namespace Scion.Application.Tests.Creating;

internal sealed class InMemoryInstructionFileCreation : IInstructionFileCreator
{
    private InstructionFileCreateFailure? _failure;

    public string? Content { get; private set; }

    public Location? Location { get; private set; }

    public Agent? Agent { get; private set; }

    public string RelativePath { get; set; } = "CLAUDE.md";

    public void FailWith(InstructionFileCreateFailure failure) => _failure = failure;

    public Task<string> CreateAsync(Location location, Agent agent, string content, CancellationToken cancellationToken)
    {
        if (_failure is { } failure)
        {
            throw new InstructionFileCreateFailedException(failure);
        }

        Location = location;
        Agent = agent;
        Content = content;
        return Task.FromResult(RelativePath);
    }
}
