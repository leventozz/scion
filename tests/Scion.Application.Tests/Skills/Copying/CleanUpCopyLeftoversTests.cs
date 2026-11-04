using Scion.Application.Projects;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Skills.Copying;

public class CleanUpCopyLeftoversTests
{
    private static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly InMemoryFolderProbe _probe = new();
    private readonly RecordingLeftovers _leftovers = new();
    private readonly CleanUpCopyLeftovers _cleanUp;

    public CleanUpCopyLeftoversTests() => _cleanUp = new CleanUpCopyLeftovers(new StartupLocations(_registry, _probe), _leftovers);

    [Fact]
    public async Task Cleans_personal_and_every_registered_project_for_both_agents()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        await _cleanUp.ExecuteAsync();

        Assert.Equal(
            [
                new AgentLocation(new Location.Personal(), Agent.ClaudeCode),
                new AgentLocation(new Location.Personal(), Agent.Codex),
                new AgentLocation(new Location.Project("/work/api"), Agent.ClaudeCode),
                new AgentLocation(new Location.Project("/work/api"), Agent.Codex),
            ],
            _leftovers.Cleaned);
    }

    [Fact]
    public async Task Skips_a_project_whose_folder_no_longer_exists()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _probe.MarkMissing("/work/gone");

        await _cleanUp.ExecuteAsync();

        Assert.DoesNotContain(_leftovers.Cleaned, place => place.Location is Location.Project);
    }

    [Fact]
    public async Task Still_cleans_personal_when_the_project_list_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        await _cleanUp.ExecuteAsync();

        Assert.Equal(2, _leftovers.Cleaned.Count);
        Assert.All(_leftovers.Cleaned, place => Assert.IsType<Location.Personal>(place.Location));
    }

    private sealed class RecordingLeftovers : ISkillCopyLeftovers
    {
        public List<AgentLocation> Cleaned { get; } = [];

        public Task CleanUpAsync(AgentLocation place, CancellationToken cancellationToken)
        {
            Cleaned.Add(place);
            return Task.CompletedTask;
        }
    }
}
