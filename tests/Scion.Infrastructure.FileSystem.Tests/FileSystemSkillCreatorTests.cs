using System.Text;
using Scion.Application.Creating;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillCreatorTests : IDisposable
{
    private const string SkillFile = "---\nname: code-review\ndescription: Açıklama.\n---\n\n## When to use\n";

    private readonly TempDirectory _project = new();

    public void Dispose() => _project.Dispose();

    private AgentLocation Place(Agent agent = Agent.ClaudeCode) => new(new Location.Project(_project.Path), agent);

    private string ClaudeSkills => Path.Combine(_project.Path, ".claude", "skills");

    private string ClaudeScratch => Path.Combine(_project.Path, ".claude", ".scion-tmp");

    private FileSystemSkillCreator Creator(Action<string, string>? moveDirectory = null) =>
        new(_project.Path, [new ClaudeCodeLayout(), new CodexLayout()], moveDirectory ?? Directory.Move);

    private Task CreateAsync(string folderName = "code-review", AgentLocation? place = null, Action<string, string>? moveDirectory = null) =>
        Creator(moveDirectory).CreateAsync(place ?? Place(), folderName, SkillFile, CancellationToken.None);

    private static async Task<SkillCreateFailure> FailureOf(Task create) =>
        (await Assert.ThrowsAsync<SkillCreateFailedException>(() => create)).Failure;

    [Fact]
    public async Task Writes_SKILL_md_as_UTF8_without_a_BOM_and_with_the_given_line_endings()
    {
        await CreateAsync();

        var bytes = await File.ReadAllBytesAsync(Path.Combine(ClaudeSkills, "code-review", "SKILL.md"));
        Assert.Equal(new UTF8Encoding(false).GetBytes(SkillFile), bytes);
    }

    [Fact]
    public async Task Creates_the_skills_folder_when_the_agent_has_none_yet()
    {
        Assert.False(Directory.Exists(ClaudeSkills));

        await CreateAsync();

        Assert.True(File.Exists(Path.Combine(ClaudeSkills, "code-review", "SKILL.md")));
    }

    [Fact]
    public async Task Leaves_no_scratch_folder_behind()
    {
        await CreateAsync();

        Assert.False(Directory.Exists(ClaudeScratch));
        Assert.Equal(["code-review"], Directory.GetDirectories(ClaudeSkills).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Does_not_remove_a_scratch_folder_that_holds_something_else()
    {
        _project.WriteFile(".claude/.scion-tmp/old-docker-0123456789abcdef0123456789abcdef/SKILL.md", "# Parked\n");

        await CreateAsync();

        Assert.True(File.Exists(Path.Combine(ClaudeScratch, "old-docker-0123456789abcdef0123456789abcdef", "SKILL.md")));
    }

    [Fact]
    public async Task Uses_the_skills_folder_of_the_agent()
    {
        await CreateAsync(place: Place(Agent.Codex));

        Assert.True(File.Exists(Path.Combine(_project.Path, ".agents", "skills", "code-review", "SKILL.md")));
    }

    [Fact]
    public async Task Personal_lives_under_the_home_directory()
    {
        await CreateAsync(place: new AgentLocation(new Location.Personal(), Agent.ClaudeCode));

        Assert.True(File.Exists(Path.Combine(ClaudeSkills, "code-review", "SKILL.md")));
    }

    [Fact]
    public async Task Never_touches_a_folder_that_is_already_there()
    {
        _project.WriteFile(".claude/skills/code-review/SKILL.md", "# Mine\n");
        _project.WriteFile(".claude/skills/code-review/scripts/run.sh", "echo mine\n");

        var failure = await FailureOf(CreateAsync());

        Assert.Equal(SkillCreateFailure.AlreadyExists, failure);
        Assert.Equal("# Mine\n", File.ReadAllText(Path.Combine(ClaudeSkills, "code-review", "SKILL.md")));
        Assert.Equal("echo mine\n", File.ReadAllText(Path.Combine(ClaudeSkills, "code-review", "scripts", "run.sh")));
        Assert.False(Directory.Exists(ClaudeScratch));
    }

    [Fact]
    public async Task An_empty_folder_of_that_name_counts_as_existing_too()
    {
        Directory.CreateDirectory(Path.Combine(ClaudeSkills, "code-review"));

        var failure = await FailureOf(CreateAsync());

        Assert.Equal(SkillCreateFailure.AlreadyExists, failure);
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ClaudeSkills, "code-review")));
    }

    [Fact]
    public async Task A_folder_that_appears_during_the_move_is_an_existing_skill_and_the_staging_goes()
    {
        var failure = await FailureOf(CreateAsync(moveDirectory: (_, target) =>
        {
            Directory.CreateDirectory(target);
            throw new IOException("The target appeared.");
        }));

        Assert.Equal(SkillCreateFailure.AlreadyExists, failure);
        Assert.False(Directory.Exists(ClaudeScratch));
    }

    [Fact]
    public async Task A_move_that_fails_for_another_reason_is_a_failed_write_and_leaves_nothing()
    {
        var failure = await FailureOf(CreateAsync(moveDirectory: (_, _) => throw new IOException("Disk full.")));

        Assert.Equal(SkillCreateFailure.Other, failure);
        Assert.False(Directory.Exists(Path.Combine(ClaudeSkills, "code-review")));
        Assert.False(Directory.Exists(ClaudeScratch));
    }

    [Fact]
    public async Task A_move_that_is_not_allowed_is_access_denied()
    {
        var failure = await FailureOf(CreateAsync(moveDirectory: (_, _) => throw new UnauthorizedAccessException()));

        Assert.Equal(SkillCreateFailure.AccessDenied, failure);
        Assert.False(Directory.Exists(ClaudeScratch));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task A_name_that_is_not_one_folder_is_refused_before_anything_is_written(string folderName)
    {
        var failure = await FailureOf(CreateAsync(folderName));

        Assert.Equal(SkillCreateFailure.Other, failure);
        Assert.False(Directory.Exists(Path.Combine(_project.Path, ".claude")));
    }
}
