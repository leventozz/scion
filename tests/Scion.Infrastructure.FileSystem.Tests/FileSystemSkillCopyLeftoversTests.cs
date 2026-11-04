using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillCopyLeftoversTests : IDisposable
{
    private readonly TempDirectory _project = new();

    public void Dispose() => _project.Dispose();

    private AgentLocation Place(Agent agent = Agent.ClaudeCode) => new(new Location.Project(_project.Path), agent);

    private Task CleanUpAsync(Agent agent = Agent.ClaudeCode) =>
        new FileSystemSkillCopyLeftovers(_project.Path, [new ClaudeCodeLayout(), new CodexLayout()])
            .CleanUpAsync(Place(agent), CancellationToken.None);

    private string InProject(string relativePath) => Path.Combine(_project.Path, relativePath);

    private const string Guid1 = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Removes_leftover_staging_folders_and_the_scratch_directory()
    {
        _project.WriteFile($".claude/.scion-tmp/tmp-docker-{Guid1}/SKILL.md", "half a copy\n");

        await CleanUpAsync();

        Assert.False(Directory.Exists(InProject(".claude/.scion-tmp")));
    }

    [Fact]
    public async Task Removes_an_old_folder_when_the_skill_is_in_place_again()
    {
        _project.WriteFile(".claude/skills/docker/SKILL.md", "# Current\n");
        _project.WriteFile($".claude/.scion-tmp/old-docker-{Guid1}/SKILL.md", "# Previous\n");

        await CleanUpAsync();

        Assert.False(Directory.Exists(InProject(".claude/.scion-tmp")));
        Assert.Equal("# Current\n", File.ReadAllText(InProject(".claude/skills/docker/SKILL.md")));
    }

    [Fact]
    public async Task Removes_leftover_edit_files_and_the_scratch_directory()
    {
        _project.WriteFile($".claude/.scion-tmp/edit-{Guid1}", "half a save");

        await CleanUpAsync();

        Assert.False(Directory.Exists(InProject(".claude/.scion-tmp")));
    }

    [Fact]
    public async Task Removes_a_leftover_edit_file_but_keeps_anything_else_in_the_scratch_directory()
    {
        _project.WriteFile($".claude/.scion-tmp/edit-{Guid1}", "half a save");
        _project.WriteFile(".claude/.scion-tmp/notes.txt", "not ours");

        await CleanUpAsync();

        Assert.False(File.Exists(InProject($".claude/.scion-tmp/edit-{Guid1}")));
        Assert.True(File.Exists(InProject(".claude/.scion-tmp/notes.txt")));
    }

    [Fact]
    public async Task Puts_an_old_folder_back_when_its_skill_is_missing_instead_of_deleting_the_only_copy()
    {
        _project.WriteFile($".claude/.scion-tmp/old-docker-{Guid1}/SKILL.md", "# Previous\n");
        _project.WriteFile($".claude/.scion-tmp/old-docker-{Guid1}/scripts/run.sh", "echo run\n");

        await CleanUpAsync();

        Assert.Equal("# Previous\n", File.ReadAllText(InProject(".claude/skills/docker/SKILL.md")));
        Assert.Equal("echo run\n", File.ReadAllText(InProject(".claude/skills/docker/scripts/run.sh")));
        Assert.False(Directory.Exists(InProject(".claude/.scion-tmp")));
    }

    [Fact]
    public async Task Cleans_the_scratch_directory_of_the_given_agent_only()
    {
        _project.WriteFile($".claude/.scion-tmp/tmp-docker-{Guid1}/SKILL.md", "claude\n");
        _project.WriteFile($".agents/.scion-tmp/tmp-docker-{Guid1}/SKILL.md", "codex\n");

        await CleanUpAsync(Agent.Codex);

        Assert.False(Directory.Exists(InProject(".agents/.scion-tmp")));
        Assert.True(Directory.Exists(InProject(".claude/.scion-tmp")));
    }

    [Fact]
    public async Task Leaves_entries_it_did_not_create_alone_and_keeps_the_scratch_directory_then()
    {
        _project.WriteFile(".claude/.scion-tmp/notes.txt", "not ours\n");
        _project.WriteFile($".claude/.scion-tmp/tmp-docker-{Guid1}/SKILL.md", "half a copy\n");

        await CleanUpAsync();

        Assert.Equal(["notes.txt"], Directory.GetFileSystemEntries(InProject(".claude/.scion-tmp")).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Does_nothing_and_creates_nothing_when_there_is_no_scratch_directory()
    {
        await CleanUpAsync();

        Assert.Empty(Directory.GetFileSystemEntries(_project.Path));
    }
}
