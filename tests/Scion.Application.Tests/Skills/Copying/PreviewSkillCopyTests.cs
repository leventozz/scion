using Scion.Application.Projects;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Skills.Copying;

public class PreviewSkillCopyTests
{
    private static readonly AgentLocation Source = new(new Location.Project("/work/api"), Agent.ClaudeCode);
    private static readonly AgentLocation Target = new(new Location.Project("/work/web"), Agent.ClaudeCode);

    private readonly InMemoryFolderProbe _probe = new();
    private readonly InMemorySkillFolders _folders = new();
    private readonly PreviewSkillCopy _preview;

    public PreviewSkillCopyTests() =>
        _preview = new PreviewSkillCopy(_probe, _folders, _folders, new PathComparison(StringComparison.Ordinal));

    [Fact]
    public async Task Reports_the_selected_skills_that_already_exist_in_the_target()
    {
        _folders.Add(Target, "docker", "testing");

        var result = await _preview.ExecuteAsync(Source, Target, ["docker", "deploy", "testing"]);

        Assert.Equal(["docker", "testing"], result.Value.Different);
    }

    [Fact]
    public async Task Tells_apart_a_skill_with_different_content_from_an_identical_one()
    {
        _folders.Add(Source, "docker", "testing");
        _folders.Add(Target, "docker", "testing");
        _folders.SetContent(Source, "testing", "same");
        _folders.SetContent(Target, "testing", "same");

        var result = await _preview.ExecuteAsync(Source, Target, ["docker", "testing"]);

        Assert.Equal(["docker"], result.Value.Different);
        Assert.Equal(["testing"], result.Value.Identical);
    }

    [Fact]
    public async Task Treats_a_skill_that_cannot_be_compared_as_different()
    {
        _folders.Add(Source, "docker");
        _folders.Add(Target, "docker");
        _folders.SetContent(Source, "docker", "same");
        _folders.SetContent(Target, "docker", "same");
        _folders.FailToCompare("docker");

        var result = await _preview.ExecuteAsync(Source, Target, ["docker"]);

        Assert.Equal(["docker"], result.Value.Different);
        Assert.Empty(result.Value.Identical);
    }

    [Fact]
    public async Task Reports_nothing_when_the_target_has_none_of_the_selection()
    {
        _folders.Add(Target, "docker");

        var result = await _preview.ExecuteAsync(Source, Target, ["deploy"]);

        Assert.Empty(result.Value.Different);
        Assert.Empty(result.Value.Identical);
    }

    [Fact]
    public async Task Compares_folder_names_with_the_injected_case_rule()
    {
        _folders.Add(Target, "Docker");
        var ignoringCase = new PreviewSkillCopy(_probe, _folders, _folders, new PathComparison(StringComparison.OrdinalIgnoreCase));

        var result = await ignoringCase.ExecuteAsync(Source, Target, ["docker"]);

        Assert.Equal(["docker"], result.Value.Different);
    }

    [Fact]
    public async Task Fails_when_the_target_project_folder_is_missing()
    {
        _probe.MarkMissing("/work/web");

        var result = await _preview.ExecuteAsync(Source, Target, ["docker"]);

        Assert.Equal(CopySkillsError.TargetFolderMissing, result.Error);
    }

    [Fact]
    public async Task Personal_has_no_folder_to_go_missing()
    {
        var preview = new PreviewSkillCopy(new NoFolderExists(), _folders, _folders, new PathComparison(StringComparison.Ordinal));
        var personal = new AgentLocation(new Location.Personal(), Agent.Codex);

        var result = await preview.ExecuteAsync(Source, personal, ["docker"]);

        Assert.True(result.IsSuccess);
    }

    private sealed class NoFolderExists : IFolderProbe
    {
        public bool Exists(string path) => false;
    }
}
