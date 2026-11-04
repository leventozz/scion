using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

public class RenameProjectTests
{
    private static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly RenameProject _renameProject;

    public RenameProjectTests() => _renameProject = new RenameProject(_registry);

    [Fact]
    public async Task Saves_the_new_name_and_keeps_the_path_and_the_opened_time()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var result = await _renameProject.ExecuteAsync("/work/api", "Our API");

        Assert.Equal(new RenamedProject("/work/api", "Our API"), result.Value);
        Assert.Equal([new RegisteredProject("/work/api", "Our API", Noon)], _registry.Projects);
    }

    [Fact]
    public async Task Trims_the_ends_of_the_name()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var result = await _renameProject.ExecuteAsync("/work/api", "  Our API  ");

        Assert.Equal("Our API", result.Value.DisplayName);
    }

    [Fact]
    public async Task Rejects_a_name_that_is_empty_after_trimming_and_saves_nothing()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var result = await _renameProject.ExecuteAsync("/work/api", "   ");

        Assert.IsType<RenameProjectError.NameEmpty>(result.Error);
        Assert.Equal(0, _registry.SaveCount);
    }

    [Fact]
    public async Task Does_not_save_when_the_name_is_unchanged()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var result = await _renameProject.ExecuteAsync("/work/api", " api ");

        Assert.Equal(new RenamedProject("/work/api", "api"), result.Value);
        Assert.Equal(0, _registry.SaveCount);
    }

    [Fact]
    public async Task Fails_with_not_registered_for_an_unknown_path()
    {
        var result = await _renameProject.ExecuteAsync("/work/unknown", "name");

        Assert.IsType<RenameProjectError.NotRegistered>(result.Error);
    }

    [Fact]
    public async Task Fails_with_unreadable_when_the_registry_file_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        var result = await _renameProject.ExecuteAsync("/work/api", "name");

        var error = Assert.IsType<RenameProjectError.Registry>(result.Error);
        Assert.Equal(new RegistryFailure(RegistryFailureKind.Unreadable, InMemoryProjectRegistry.RegistryFile), error.Failure);
    }

    [Fact]
    public async Task Fails_with_write_failed_when_the_new_name_cannot_be_saved()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _registry.FailsToWrite = true;

        var result = await _renameProject.ExecuteAsync("/work/api", "Our API");

        var error = Assert.IsType<RenameProjectError.Registry>(result.Error);
        Assert.Equal(RegistryFailureKind.WriteFailed, error.Failure.Kind);
    }
}
