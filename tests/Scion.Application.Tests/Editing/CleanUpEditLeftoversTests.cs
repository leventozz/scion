using Scion.Application.Editing;
using Scion.Application.Projects;
using Scion.Application.Tests.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Editing;

public class CleanUpEditLeftoversTests
{
    private static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly InMemoryFolderProbe _probe = new();
    private readonly RecordingLeftovers _leftovers = new();
    private readonly CleanUpEditLeftovers _cleanUp;

    public CleanUpEditLeftoversTests() =>
        _cleanUp = new CleanUpEditLeftovers(new StartupLocations(_registry, _probe), _leftovers);

    [Fact]
    public async Task Cleans_personal_and_every_registered_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        await _cleanUp.ExecuteAsync();

        Assert.Equal([new Location.Personal(), new Location.Project("/work/api")], _leftovers.Cleaned);
    }

    [Fact]
    public async Task Skips_a_project_whose_folder_no_longer_exists()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _probe.MarkMissing("/work/gone");

        await _cleanUp.ExecuteAsync();

        Assert.Equal([new Location.Personal()], _leftovers.Cleaned);
    }

    [Fact]
    public async Task Still_cleans_personal_when_the_project_list_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        await _cleanUp.ExecuteAsync();

        Assert.Equal([new Location.Personal()], _leftovers.Cleaned);
    }

    private sealed class RecordingLeftovers : IInstructionFileLeftovers
    {
        public List<Location> Cleaned { get; } = [];

        public Task CleanUpAsync(Location location, CancellationToken cancellationToken)
        {
            Cleaned.Add(location);
            return Task.CompletedTask;
        }
    }
}
