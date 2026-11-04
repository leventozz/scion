using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemInstructionFileLeftoversTests : IDisposable
{
    private const string Guid1 = "0123456789abcdef0123456789abcdef";

    private readonly TempDirectory _home = new();
    private readonly TempDirectory _project = new();

    public void Dispose()
    {
        _home.Dispose();
        _project.Dispose();
    }

    private Task CleanUpAsync(Location location) =>
        new FileSystemInstructionFileLeftovers(_home.Path, [new ClaudeCodeLayout(), new CodexLayout()])
            .CleanUpAsync(location, CancellationToken.None);

    private Task CleanUpProjectAsync() => CleanUpAsync(new Location.Project(_project.Path));

    private string InProject(string relativePath) => Path.Combine(_project.Path, relativePath);

    [Theory]
    [InlineData($".CLAUDE.md.scion-{Guid1}.tmp")]
    [InlineData($".AGENTS.md.scion-{Guid1}.tmp")]
    [InlineData($".claude/.CLAUDE.md.scion-{Guid1}.tmp")]
    public async Task Removes_a_leftover_temp_file_beside_a_project_instruction_file(string relativePath)
    {
        _project.WriteFile(relativePath, "half a save");

        await CleanUpProjectAsync();

        Assert.False(File.Exists(InProject(relativePath)));
    }

    [Theory]
    [InlineData($".claude/.CLAUDE.md.scion-{Guid1}.tmp")]
    [InlineData($".codex/.AGENTS.md.scion-{Guid1}.tmp")]
    public async Task Removes_a_leftover_temp_file_beside_a_personal_instruction_file(string relativePath)
    {
        _home.WriteFile(relativePath, "half a save");

        await CleanUpAsync(new Location.Personal());

        Assert.False(File.Exists(Path.Combine(_home.Path, relativePath)));
    }

    [Fact]
    public async Task Keeps_the_instruction_files_and_everything_that_is_not_a_scion_temp_file()
    {
        string[] kept =
        [
            "CLAUDE.md",
            ".claude/CLAUDE.md",
            "AGENTS.md",
            ".CLAUDE.md.scion-tmp",
            $".notes.md.scion-{Guid1}.tmp",
            $".CLAUDE.md.scion-{Guid1}.bak",
            ".CLAUDE.md.scion-123.tmp",
            $"docs/.CLAUDE.md.scion-{Guid1}.tmp",
            $".claude/skills/pdf/.CLAUDE.md.scion-{Guid1}.tmp",
        ];
        foreach (var path in kept)
        {
            _project.WriteFile(path, "mine");
        }

        await CleanUpProjectAsync();

        Assert.All(kept, path => Assert.True(File.Exists(InProject(path)), path));
    }

    [Fact]
    public async Task Does_not_create_folders_and_does_not_fail_when_there_is_nothing_to_clean()
    {
        await CleanUpProjectAsync();
        await CleanUpAsync(new Location.Personal());

        Assert.Empty(Directory.GetFileSystemEntries(_project.Path));
        Assert.Empty(Directory.GetFileSystemEntries(_home.Path));
    }

    [Fact]
    public async Task Does_not_fail_when_the_project_folder_is_gone()
    {
        await CleanUpAsync(new Location.Project(Path.Combine(_project.Path, "missing")));
    }
}
