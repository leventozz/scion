using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.Application.Tests.Skills;

public class ListSkillsTests
{
    private static readonly Location AProject = new Location.Project(@"C:\projects\a");
    private static readonly Location Personal = new Location.Personal();

    private readonly InMemorySkillFolderReader _reader = new();

    [Fact]
    public async Task Returns_an_empty_list_when_the_location_has_no_skills_for_the_agent()
    {
        var skills = await SkillsOf(AProject, Agent.ClaudeCode);

        Assert.Empty(skills);
    }

    [Fact]
    public async Task Returns_the_name_and_description_of_a_valid_skill_without_warnings()
    {
        GivenSkills(new SkillFolder("docker-compose", "docker-compose", "Runs compose files."));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal("docker-compose", skill.Name);
        Assert.Equal("docker-compose", skill.FolderName);
        Assert.Equal("Runs compose files.", skill.Description);
        Assert.Empty(skill.Warnings);
    }

    [Fact]
    public async Task Lists_skills_alphabetically_by_name()
    {
        GivenSkills(
            new SkillFolder("c", "c", "Third."),
            new SkillFolder("a", "a", "First."),
            new SkillFolder("b", "b", "Second."));

        var skills = await SkillsOf(AProject, Agent.ClaudeCode);

        Assert.Equal(["a", "b", "c"], skills.Select(skill => skill.Name));
    }

    [Theory]
    [InlineData("", SkillWarning.NameEmpty)]
    [InlineData(null, SkillWarning.NameEmpty)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", SkillWarning.NameTooLong)]
    [InlineData("Docker", SkillWarning.NameHasUppercaseLetter)]
    [InlineData("-docker", SkillWarning.NameHasLeadingOrTrailingHyphen)]
    [InlineData("docker--compose", SkillWarning.NameHasConsecutiveHyphens)]
    [InlineData("docker_compose", SkillWarning.NameHasDisallowedCharacter)]
    public async Task Lists_a_skill_with_an_invalid_name_with_a_warning(string? name, SkillWarning expected)
    {
        GivenSkills(new SkillFolder(name ?? "", name, "Runs compose files."));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal([expected], skill.Warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Shows_a_skill_without_a_frontmatter_name_under_its_folder_name(string? name)
    {
        GivenSkills(new SkillFolder("docker-compose", name, "Runs compose files."));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal("docker-compose", skill.Name);
        Assert.Equal([SkillWarning.NameEmpty], skill.Warnings);
    }

    [Fact]
    public async Task Lists_a_skill_whose_name_does_not_match_its_folder_name_with_a_warning()
    {
        GivenSkills(new SkillFolder("compose", "docker-compose", "Runs compose files."));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal("docker-compose", skill.Name);
        Assert.Equal("compose", skill.FolderName);
        Assert.Equal([SkillWarning.NameDoesNotMatchFolder], skill.Warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Lists_a_skill_with_a_missing_description_with_a_warning(string? description)
    {
        GivenSkills(new SkillFolder("docker-compose", "docker-compose", description));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal("", skill.Description);
        Assert.Equal([SkillWarning.DescriptionMissing], skill.Warnings);
    }

    [Fact]
    public async Task Lists_a_skill_with_a_description_longer_than_1024_characters_with_a_warning()
    {
        var description = new string('a', 1025);
        GivenSkills(new SkillFolder("docker-compose", "docker-compose", description));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal(description, skill.Description);
        Assert.Equal([SkillWarning.DescriptionTooLong], skill.Warnings);
    }

    [Fact]
    public async Task A_skill_can_be_deleted_unless_its_folder_is_linked()
    {
        GivenSkills(
            new SkillFolder("own", "own", "Lives here."),
            new SkillFolder("shared", "shared", "Lives elsewhere.", IsLinked: true));

        var skills = await SkillsOf(AProject, Agent.ClaudeCode);

        Assert.True(skills.Single(skill => skill.FolderName == "own").CanBeDeleted);
        Assert.False(skills.Single(skill => skill.FolderName == "shared").CanBeDeleted);
    }

    [Fact]
    public async Task Lists_a_skill_with_unreadable_frontmatter_under_its_folder_name_with_only_that_warning()
    {
        GivenSkills(new SkillFolder("docker-compose", null, null, FrontmatterUnreadable: true));

        var skill = Assert.Single(await SkillsOf(AProject, Agent.ClaudeCode));

        Assert.Equal("docker-compose", skill.Name);
        Assert.Equal("docker-compose", skill.FolderName);
        Assert.Equal("", skill.Description);
        Assert.Equal([SkillWarning.FrontmatterUnreadable], skill.Warnings);
    }

    [Fact]
    public async Task Lists_only_the_skills_of_the_requested_location_and_agent()
    {
        _reader.Add(AProject, Agent.ClaudeCode, new SkillFolder("claude-project", "claude-project", "Claude, project."));
        _reader.Add(AProject, Agent.Codex, new SkillFolder("codex-project", "codex-project", "Codex, project."));
        _reader.Add(Personal, Agent.Codex, new SkillFolder("codex-personal", "codex-personal", "Codex, personal."));

        var skills = await SkillsOf(AProject, Agent.Codex);

        Assert.Equal(["codex-project"], skills.Select(skill => skill.Name));
    }

    private void GivenSkills(params SkillFolder[] skillFolders) =>
        _reader.Add(AProject, Agent.ClaudeCode, skillFolders);

    private Task<IReadOnlyList<SkillSummary>> SkillsOf(Location location, Agent agent) =>
        new ListSkills(_reader).ExecuteAsync(location, agent);
}
