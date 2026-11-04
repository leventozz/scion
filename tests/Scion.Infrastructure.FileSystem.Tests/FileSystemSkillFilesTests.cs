using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillFilesTests
{
    private static readonly IAgentLayout[] Layouts = [new ClaudeCodeLayout(), new CodexLayout()];

    [Fact]
    public async Task A_missing_skill_folder_lists_as_null()
    {
        using var project = new TempDirectory();

        Assert.Null(await ListAsync(project, "pdf"));
    }

    [Fact]
    public async Task Lists_every_file_at_every_depth_as_slash_separated_relative_paths()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        project.WriteFile(".claude/skills/pdf/scripts/run.sh", "echo");
        project.WriteFile(".claude/skills/pdf/references/deep/notes.md", "notes");

        var listing = await ListAsync(project, "pdf");

        Assert.Equal(
            ["SKILL.md", "references/deep/notes.md", "scripts/run.sh"],
            listing!.FilePaths.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Carries_the_absolute_path_of_the_skill_folder()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");

        var listing = await ListAsync(project, "pdf");

        Assert.Equal(Path.Combine(project.Path, ".claude", "skills", "pdf"), listing!.FolderPath);
    }

    [Fact]
    public async Task Hides_files_and_folders_whose_names_start_with_a_dot_at_every_depth()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        project.WriteFile(".claude/skills/pdf/.env", "SECRET=1");
        project.WriteFile(".claude/skills/pdf/.git/config", "[core]");
        project.WriteFile(".claude/skills/pdf/scripts/run.sh", "echo");
        project.WriteFile(".claude/skills/pdf/scripts/.DS_Store", "x");
        project.WriteFile(".claude/skills/pdf/scripts/.cache/blob", "x");

        var listing = await ListAsync(project, "pdf");

        Assert.Equal(["SKILL.md", "scripts/run.sh"], listing!.FilePaths.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Each_agent_lists_its_own_skills_directory()
    {
        using var project = new TempDirectory();
        project.WriteFile(".agents/skills/pdf/SKILL.md", "# Codex PDF");

        var codex = await Files().ListAsync(new Location.Project(project.Path), Agent.Codex, "pdf", CancellationToken.None);
        var claude = await Files().ListAsync(new Location.Project(project.Path), Agent.ClaudeCode, "pdf", CancellationToken.None);

        Assert.NotNull(codex);
        Assert.Null(claude);
    }

    [Fact]
    public async Task Personal_skills_are_listed_from_the_injected_home_directory()
    {
        using var home = new TempDirectory();
        home.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");

        var listing = await new FileSystemSkillFiles(home.Path, Layouts)
            .ListAsync(new Location.Personal(), Agent.ClaudeCode, "pdf", CancellationToken.None);

        Assert.Equal(["SKILL.md"], listing!.FilePaths);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task A_folder_name_that_is_not_a_single_segment_is_refused(string folderName)
    {
        using var project = new TempDirectory();

        await Assert.ThrowsAsync<FileViewFailedException>(() => ListAsync(project, folderName));
        await Assert.ThrowsAsync<FileViewFailedException>(() => ReadAsync(project, folderName, "SKILL.md"));
    }

    [Fact]
    public async Task Reads_the_bytes_and_the_length_of_a_file_and_names_its_containing_folder()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/scripts/run.sh", "echo hi");

        var content = await ReadAsync(project, "pdf", "scripts/run.sh");

        Assert.Equal("echo hi"u8.ToArray(), content.Read.Value.Head);
        Assert.Equal(7, content.Read.Value.Length);
        Assert.Equal(Path.Combine(project.Path, ".claude", "skills", "pdf", "scripts"), content.FolderPath);
    }

    [Fact]
    public async Task Reads_at_most_the_limit_plus_one_byte_but_reports_the_real_length()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/big.txt", new string('a', 100));

        var content = await ReadAsync(project, "pdf", "big.txt", maxBytes: 10);

        Assert.Equal(11, content.Read.Value.Head.Length);
        Assert.Equal(100, content.Read.Value.Length);
    }

    [Fact]
    public async Task A_missing_file_has_no_bytes()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");

        var content = await ReadAsync(project, "pdf", "gone.md");

        Assert.False(content.Read.IsSuccess);
        Assert.Equal(FileReadError.Missing, content.Read.Error);
    }

    [Fact]
    public async Task A_file_another_process_holds_open_for_writing_can_still_be_read()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        var path = Path.Combine(project.Path, ".claude", "skills", "pdf", "SKILL.md");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        var content = await ReadAsync(project, "pdf", "SKILL.md");

        Assert.Equal("# PDF"u8.ToArray(), content.Read.Value.Head);
    }

    [Fact]
    public async Task A_file_that_is_locked_exclusively_has_no_bytes()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        var path = Path.Combine(project.Path, ".claude", "skills", "pdf", "SKILL.md");
        using var lockedFile = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var content = await ReadAsync(project, "pdf", "SKILL.md");

        Assert.False(content.Read.IsSuccess);
        Assert.Equal(FileReadError.Unreadable, content.Read.Error);
        Assert.Equal(Path.GetDirectoryName(path), content.FolderPath);
    }

    [Theory]
    [InlineData("../other/SKILL.md")]
    [InlineData("scripts/../../other/SKILL.md")]
    [InlineData("/etc/passwd")]
    public async Task A_path_that_leaves_the_skill_folder_is_refused(string relativePath)
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        project.WriteFile(".claude/skills/other/SKILL.md", "# Other");

        await Assert.ThrowsAsync<FileViewFailedException>(() => ReadAsync(project, "pdf", relativePath));
    }

    private static FileSystemSkillFiles Files() => new("/unused-home", Layouts);

    private static Task<SkillFolderListing?> ListAsync(TempDirectory project, string folderName) =>
        Files().ListAsync(new Location.Project(project.Path), Agent.ClaudeCode, folderName, CancellationToken.None);

    private static Task<SkillFileContent> ReadAsync(
        TempDirectory project, string folderName, string relativePath, int maxBytes = FileLimit) =>
        Files().ReadAsync(new Location.Project(project.Path), Agent.ClaudeCode, folderName, relativePath, maxBytes, CancellationToken.None);

    private const int FileLimit = 1_048_576;
}
