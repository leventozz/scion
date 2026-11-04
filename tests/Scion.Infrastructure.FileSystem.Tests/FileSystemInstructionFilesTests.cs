using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemInstructionFilesTests
{
    private const int FileLimit = 1_048_576;

    private static readonly IInstructionFileLayout[] Layouts = [new ClaudeCodeLayout(), new CodexLayout()];

    [Fact]
    public async Task A_project_without_instruction_files_gives_an_empty_list()
    {
        using var project = new TempDirectory();

        Assert.Empty(await ReadProjectAsync(project, Agent.ClaudeCode));
    }

    [Fact]
    public async Task A_missing_project_folder_gives_an_empty_list()
    {
        var files = await new FileSystemInstructionFiles("/unused", Layouts)
            .ReadAsync(new Location.Project("/does/not/exist"), Agent.ClaudeCode, FileLimit, CancellationToken.None);

        Assert.Empty(files);
    }

    [Fact]
    public async Task Claude_reads_both_project_files_in_convention_order()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/CLAUDE.md", "# Nested");
        project.WriteFile("CLAUDE.md", "# Root");

        var files = await ReadProjectAsync(project, Agent.ClaudeCode);

        Assert.Equal(["CLAUDE.md", ".claude/CLAUDE.md"], files.Select(file => file.RelativePath));
        Assert.Equal("# Root"u8.ToArray(), files[0].Read.Value.Head);
        Assert.Equal(project.Path, files[0].FolderPath);
        Assert.Equal(Path.Combine(project.Path, ".claude"), files[1].FolderPath);
    }

    [Fact]
    public async Task Claude_reads_only_the_nested_file_when_the_root_file_is_absent()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/CLAUDE.md", "# Nested");

        var files = await ReadProjectAsync(project, Agent.ClaudeCode);

        Assert.Equal([".claude/CLAUDE.md"], files.Select(file => file.RelativePath));
    }

    [Fact]
    public async Task A_file_whose_name_differs_only_in_case_does_not_count()
    {
        using var project = new TempDirectory();
        project.WriteFile("claude.md", "# Wrong case");
        project.WriteFile(".claude/Claude.md", "# Wrong case");

        Assert.Empty(await ReadProjectAsync(project, Agent.ClaudeCode));
    }

    [Fact]
    public async Task A_folder_with_the_name_of_an_instruction_file_does_not_count()
    {
        using var project = new TempDirectory();
        project.WriteFile("CLAUDE.md/readme.txt", "not a file");

        Assert.Empty(await ReadProjectAsync(project, Agent.ClaudeCode));
    }

    [Fact]
    public async Task Codex_reads_only_AGENTS_md_in_the_project_root()
    {
        using var project = new TempDirectory();
        project.WriteFile("AGENTS.md", "# Agents");
        project.WriteFile("CLAUDE.md", "# Claude");

        var files = await ReadProjectAsync(project, Agent.Codex);

        Assert.Equal(["AGENTS.md"], files.Select(file => file.RelativePath));
    }

    [Fact]
    public async Task Personal_Claude_reads_dot_claude_CLAUDE_md_from_the_home_directory()
    {
        using var home = new TempDirectory();
        home.WriteFile(".claude/CLAUDE.md", "# Mine");
        home.WriteFile("CLAUDE.md", "# Not personal");

        var files = await ReadPersonalAsync(home, Agent.ClaudeCode);

        Assert.Equal([".claude/CLAUDE.md"], files.Select(file => file.RelativePath));
        Assert.Equal(Path.Combine(home.Path, ".claude"), files[0].FolderPath);
    }

    [Fact]
    public async Task Personal_Codex_reads_dot_codex_AGENTS_md_from_the_home_directory()
    {
        using var home = new TempDirectory();
        home.WriteFile(".codex/AGENTS.md", "# Mine");
        home.WriteFile(".codex/AGENTS.override.md", "# Out of v1");
        home.WriteFile("AGENTS.md", "# Not personal");

        var files = await ReadPersonalAsync(home, Agent.Codex);

        Assert.Equal([".codex/AGENTS.md"], files.Select(file => file.RelativePath));
    }

    [Fact]
    public async Task Reads_at_most_the_limit_plus_one_byte_but_reports_the_real_length()
    {
        using var project = new TempDirectory();
        project.WriteFile("CLAUDE.md", new string('a', 100));

        var files = await new FileSystemInstructionFiles("/unused", Layouts)
            .ReadAsync(new Location.Project(project.Path), Agent.ClaudeCode, 10, CancellationToken.None);

        Assert.Equal(11, files[0].Read.Value.Head.Length);
        Assert.Equal(100, files[0].Read.Value.Length);
    }

    [Fact]
    public async Task A_file_that_is_locked_exclusively_is_listed_without_bytes()
    {
        using var project = new TempDirectory();
        project.WriteFile("CLAUDE.md", "# Root");
        using var lockedFile = new FileStream(
            Path.Combine(project.Path, "CLAUDE.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var file = Assert.Single(await ReadProjectAsync(project, Agent.ClaudeCode));

        Assert.False(file.Read.IsSuccess);
        Assert.Equal(FileReadError.Unreadable, file.Read.Error);
        Assert.Equal(project.Path, file.FolderPath);
    }

    private static Task<IReadOnlyList<InstructionFileBytes>> ReadProjectAsync(TempDirectory project, Agent agent) =>
        new FileSystemInstructionFiles("/unused", Layouts)
            .ReadAsync(new Location.Project(project.Path), agent, FileLimit, CancellationToken.None);

    private static Task<IReadOnlyList<InstructionFileBytes>> ReadPersonalAsync(TempDirectory home, Agent agent) =>
        new FileSystemInstructionFiles(home.Path, Layouts)
            .ReadAsync(new Location.Personal(), agent, FileLimit, CancellationToken.None);
}
