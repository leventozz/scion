using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

public class RemoveProjectTests
{
    private static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly RemoveProject _removeProject;

    public RemoveProjectTests() => _removeProject = new RemoveProject(_registry);

    [Fact]
    public async Task Unregisters_only_the_given_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _registry.Add(new RegisteredProject("/work/web", "web", Noon));

        var result = await _removeProject.ExecuteAsync("/work/api");

        Assert.Equal(new RemovedProject("/work/api"), result.Value);
        Assert.Equal(["/work/web"], _registry.Projects.Select(project => project.Path));
    }

    [Fact]
    public async Task Succeeds_without_changes_for_a_path_that_is_not_registered()
    {
        _registry.Add(new RegisteredProject("/work/web", "web", Noon));

        var result = await _removeProject.ExecuteAsync("/work/unknown");

        Assert.True(result.IsSuccess);
        Assert.Single(_registry.Projects);
    }

    [Fact]
    public async Task Fails_with_unreadable_when_the_registry_file_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        var result = await _removeProject.ExecuteAsync("/work/api");

        Assert.Equal(new RegistryFailure(RegistryFailureKind.Unreadable, InMemoryProjectRegistry.RegistryFile), result.Error);
    }

    [Fact]
    public async Task Fails_with_write_failed_when_the_registry_cannot_be_saved()
    {
        _registry.FailsToWrite = true;

        var result = await _removeProject.ExecuteAsync("/work/api");

        Assert.Equal(RegistryFailureKind.WriteFailed, result.Error.Kind);
    }
}
