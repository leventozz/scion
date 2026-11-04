using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

public class OpenProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly InMemoryFolderProbe _folders = new();
    private readonly OpenProject _openProject;

    public OpenProjectTests() => _openProject = new OpenProject(_registry, new FixedTimeProvider(Now), _folders);

    [Fact]
    public async Task Updates_the_last_opened_time_and_returns_the_opened_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "Our API", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var result = await _openProject.ExecuteAsync("/work/api");

        Assert.True(result.IsSuccess);
        Assert.Equal(new OpenedProject("/work/api", "Our API"), result.Value);
        Assert.Equal([new RegisteredProject("/work/api", "Our API", Now)], _registry.Projects);
    }

    [Fact]
    public async Task Fails_with_not_registered_for_an_unknown_path_and_saves_nothing()
    {
        var result = await _openProject.ExecuteAsync("/work/unknown");

        Assert.False(result.IsSuccess);
        Assert.IsType<OpenProjectError.NotRegistered>(result.Error);
        Assert.Equal(0, _registry.SaveCount);
    }

    [Fact]
    public async Task Reports_a_missing_folder_but_still_marks_the_project_as_opened()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        _folders.MarkMissing("/work/gone");

        var result = await _openProject.ExecuteAsync("/work/gone");

        Assert.Equal(new OpenedProject("/work/gone", "gone", FolderMissing: true), result.Value);
        Assert.Equal([new RegisteredProject("/work/gone", "gone", Now)], _registry.Projects);
    }

    [Fact]
    public async Task Fails_with_the_registry_failure_when_the_registry_file_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        var result = await _openProject.ExecuteAsync("/work/api");

        var error = Assert.IsType<OpenProjectError.Registry>(result.Error);
        Assert.Equal(new RegistryFailure(RegistryFailureKind.Unreadable, InMemoryProjectRegistry.RegistryFile), error.Failure);
    }

    [Fact]
    public async Task Fails_with_write_failed_when_the_opened_time_cannot_be_saved()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Now.AddDays(-1)));
        _registry.FailsToWrite = true;

        var result = await _openProject.ExecuteAsync("/work/api");

        var error = Assert.IsType<OpenProjectError.Registry>(result.Error);
        Assert.Equal(RegistryFailureKind.WriteFailed, error.Failure.Kind);
    }
}
