using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

public class RegisterProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly FixedTimeProvider _time = new(Now);
    private readonly RegisterProject _registerProject;

    public RegisterProjectTests() => _registerProject = new RegisterProject(_registry, _time);

    [Fact]
    public async Task Saves_a_new_project_named_after_its_folder_and_opened_now()
    {
        var opened = (await _registerProject.ExecuteAsync("/work/orders-api")).Value;

        Assert.Equal(new OpenedProject("/work/orders-api", "orders-api"), opened);
        Assert.Equal(
            [new RegisteredProject("/work/orders-api", "orders-api", Now)],
            _registry.Projects);
    }

    [Theory]
    [InlineData("/work/orders-api/", "orders-api")]
    [InlineData(@"C:\work\orders-api\", "orders-api")]
    [InlineData(@"C:\work\orders-api", "orders-api")]
    public async Task Names_the_project_after_the_last_path_segment_ignoring_trailing_separators(
        string path, string expectedName)
    {
        var opened = (await _registerProject.ExecuteAsync(path)).Value;

        Assert.Equal(expectedName, opened.DisplayName);
    }

    [Theory]
    [InlineData("/")]
    [InlineData(@"C:\")]
    public async Task Falls_back_to_the_path_itself_for_a_root_path(string rootPath)
    {
        var opened = (await _registerProject.ExecuteAsync(rootPath)).Value;

        Assert.Equal(rootPath, opened.DisplayName);
    }

    [Fact]
    public async Task Opens_an_already_registered_project_instead_of_adding_a_duplicate()
    {
        var earlier = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _registry.Add(new RegisteredProject("/work/api", "Our API", earlier));

        var opened = (await _registerProject.ExecuteAsync("/work/api")).Value;

        Assert.Equal(new OpenedProject("/work/api", "Our API"), opened);
        Assert.Equal([new RegisteredProject("/work/api", "Our API", Now)], _registry.Projects);
    }

    [Fact]
    public async Task Fails_with_unreadable_when_the_registry_file_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        var result = await _registerProject.ExecuteAsync("/work/api");

        Assert.Equal(new RegistryFailure(RegistryFailureKind.Unreadable, InMemoryProjectRegistry.RegistryFile), result.Error);
    }

    [Fact]
    public async Task Fails_with_write_failed_when_the_project_cannot_be_saved()
    {
        _registry.FailsToWrite = true;

        var result = await _registerProject.ExecuteAsync("/work/api");

        Assert.Equal(new RegistryFailure(RegistryFailureKind.WriteFailed, InMemoryProjectRegistry.RegistryFile), result.Error);
    }
}
