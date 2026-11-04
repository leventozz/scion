using Scion.Application.Creating;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Projects;
using Scion.Application.Tests.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Creating;

public class CreateSkillTests
{
    private static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);

    private readonly InMemoryFolderProbe _probe = new();
    private readonly InMemorySkillFolders _folders = new();
    private readonly InMemorySkillCreation _creation;
    private readonly CreateSkill _createSkill;

    public CreateSkillTests()
    {
        _creation = new InMemorySkillCreation(_folders);
        _createSkill = new CreateSkill(
            _probe, _folders, new FakeFrontmatterWriter(), _creation, new PathComparison(StringComparison.Ordinal));
    }

    [Fact]
    public async Task Creates_the_skill_from_the_template_and_describes_it()
    {
        var result = await _createSkill.ExecuteAsync(Api, "code-review", "Reviews pull requests.");

        Assert.True(result.IsSuccess);
        Assert.Equal("code-review", result.Value.Name);
        Assert.Equal("code-review", result.Value.FolderName);
        Assert.Equal("Reviews pull requests.", result.Value.Description);
        Assert.Empty(result.Value.Warnings);
        Assert.Equal(
            "---\nname: code-review\ndescription: Reviews pull requests.\n---\n\n",
            _creation.SkillFileOf(Api, "code-review"));
    }

    [Fact]
    public async Task Writes_the_description_as_one_trimmed_line()
    {
        var result = await _createSkill.ExecuteAsync(Api, "code-review", "  Reviews\npull requests.\n");

        Assert.Equal("Reviews pull requests.", result.Value.Description);
        Assert.Contains("description: Reviews pull requests.\n", _creation.SkillFileOf(Api, "code-review"));
    }

    [Fact]
    public async Task Works_in_personal_too()
    {
        var personal = new AgentLocation(new Location.Personal(), Agent.Codex);

        var result = await _createSkill.ExecuteAsync(personal, "code-review", "Reviews pull requests.");

        Assert.True(result.IsSuccess);
        Assert.True(_folders.Has(personal, "code-review"));
    }

    [Fact]
    public async Task Refuses_input_that_breaks_the_rules_and_creates_nothing()
    {
        var result = await _createSkill.ExecuteAsync(Api, "Code Review", "Reviews pull requests.");

        Assert.Equal(CreateSkillError.Invalid, result.Error);
        Assert.False(_folders.Has(Api, "Code Review"));
    }

    [Fact]
    public async Task Refuses_a_name_that_appeared_since_the_dialog_opened_and_leaves_it_alone()
    {
        _folders.Add(Api, "code-review");

        var result = await _createSkill.ExecuteAsync(Api, "code-review", "Reviews pull requests.");

        Assert.Equal(CreateSkillError.AlreadyExists, result.Error);
        Assert.Throws<KeyNotFoundException>(() => _creation.SkillFileOf(Api, "code-review"));
    }

    [Fact]
    public async Task A_project_folder_that_is_gone_creates_nothing()
    {
        _probe.MarkMissing("/work/api");

        var result = await _createSkill.ExecuteAsync(Api, "code-review", "Reviews pull requests.");

        Assert.Equal(CreateSkillError.FolderMissing, result.Error);
        Assert.False(_folders.Has(Api, "code-review"));
    }

    [Fact]
    public async Task A_folder_that_shows_up_between_the_check_and_the_write_is_an_existing_skill()
    {
        _creation.FailWith(SkillCreateFailure.AlreadyExists);

        var result = await _createSkill.ExecuteAsync(Api, "code-review", "Reviews pull requests.");

        Assert.Equal(CreateSkillError.AlreadyExists, result.Error);
    }

    [Theory]
    [InlineData(SkillCreateFailure.AccessDenied)]
    [InlineData(SkillCreateFailure.Other)]
    public async Task Any_other_write_failure_is_a_failed_write(SkillCreateFailure failure)
    {
        _creation.FailWith(failure);

        var result = await _createSkill.ExecuteAsync(Api, "code-review", "Reviews pull requests.");

        Assert.Equal(CreateSkillError.WriteFailed, result.Error);
    }
}
