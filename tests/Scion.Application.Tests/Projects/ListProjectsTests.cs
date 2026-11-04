using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Application.Tests.Skills;
using Scion.Domain;

namespace Scion.Application.Tests.Projects;

public class ListProjectsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistry _registry = new();
    private readonly InMemorySkillFolderReader _reader = new();
    private readonly InMemoryFolderProbe _folders = new();
    private readonly ListProjects _listProjects;

    public ListProjectsTests() => _listProjects = new ListProjects(_registry, new ListSkills(_reader), _folders);

    private async Task<ProjectOverview> OverviewAsync() => (await _listProjects.ExecuteAsync()).Value;

    [Fact]
    public async Task Lists_nothing_for_an_empty_registry()
    {
        var overview = await OverviewAsync();

        Assert.Empty(overview.Projects);
    }

    [Fact]
    public async Task Lists_the_most_recently_opened_project_first()
    {
        _registry.Add(new RegisteredProject("/a", "older", Noon.AddDays(-2)));
        _registry.Add(new RegisteredProject("/b", "newest", Noon));
        _registry.Add(new RegisteredProject("/c", "middle", Noon.AddDays(-1)));

        var overview = await OverviewAsync();

        Assert.Equal(["newest", "middle", "older"], overview.Projects.Select(project => project.DisplayName));
    }

    [Fact]
    public async Task Orders_projects_opened_at_the_same_time_by_display_name_ignoring_case()
    {
        _registry.Add(new RegisteredProject("/1", "beta", Noon));
        _registry.Add(new RegisteredProject("/2", "Alpha", Noon));
        _registry.Add(new RegisteredProject("/3", "charlie", Noon));

        var overview = await OverviewAsync();

        Assert.Equal(["Alpha", "beta", "charlie"], overview.Projects.Select(project => project.DisplayName));
    }

    [Fact]
    public async Task Counts_the_skills_of_each_project_per_agent()
    {
        var project = new Location.Project("/work/api");
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.Add(project, Agent.ClaudeCode, Skill("one"), Skill("two"), Skill("three"));
        _reader.Add(project, Agent.Codex, Skill("one"));

        var overview = await OverviewAsync();

        Assert.Equal(new ProjectSummary("/work/api", "api", 3, 1), Assert.Single(overview.Projects));
    }

    [Fact]
    public async Task Counts_skills_that_have_warnings_too()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.Add(new Location.Project("/work/api"), Agent.ClaudeCode,
            new SkillFolder("broken", null, null, FrontmatterUnreadable: true));

        var overview = await OverviewAsync();

        Assert.Equal(1, Assert.Single(overview.Projects).ClaudeSkillCount);
    }

    [Fact]
    public async Task Counts_zero_for_a_project_without_agent_files()
    {
        _registry.Add(new RegisteredProject("/work/empty", "empty", Noon));

        var overview = await OverviewAsync();

        Assert.Equal(new ProjectSummary("/work/empty", "empty", 0, 0), Assert.Single(overview.Projects));
    }

    [Fact]
    public async Task Counts_the_personal_skills_per_agent()
    {
        _reader.Add(new Location.Personal(), Agent.ClaudeCode, Skill("one"), Skill("two"));
        _reader.Add(new Location.Personal(), Agent.Codex, Skill("one"), Skill("two"), Skill("three"));

        var overview = await OverviewAsync();

        Assert.Equal(new PersonalSummary(2, 3), overview.Personal);
    }

    [Fact]
    public async Task Marks_a_project_whose_folder_is_missing_and_does_not_read_its_skills()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _reader.Add(new Location.Project("/work/gone"), Agent.ClaudeCode, Skill("one"));
        _folders.MarkMissing("/work/gone");

        var overview = await OverviewAsync();

        Assert.Equal(new ProjectSummary("/work/gone", "gone", 0, 0, FolderMissing: true), Assert.Single(overview.Projects));
    }

    [Fact]
    public async Task Does_not_mark_a_project_whose_folder_exists()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var overview = await OverviewAsync();

        Assert.False(Assert.Single(overview.Projects).FolderMissing);
    }

    [Fact]
    public async Task Fails_with_unreadable_when_the_registry_file_cannot_be_read()
    {
        _registry.IsUnreadable = true;

        var result = await _listProjects.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(new RegistryFailure(RegistryFailureKind.Unreadable, InMemoryProjectRegistry.RegistryFile), result.Error);
    }

    private static SkillFolder Skill(string name) => new(name, name, "A skill.");
}
