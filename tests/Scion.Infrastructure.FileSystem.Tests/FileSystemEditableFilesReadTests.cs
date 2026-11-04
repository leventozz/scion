using Scion.Application.Editing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemEditableFilesReadTests : IDisposable
{
    private readonly TempDirectory _project = new();

    public void Dispose() => _project.Dispose();

    private Location Project => new Location.Project(_project.Path);

    private static FileSystemEditableFiles Files(string home) =>
        new(home, [new ClaudeCodeLayout(), new CodexLayout()], [new ClaudeCodeLayout(), new CodexLayout()]);

    private Task<Result<FileBytes, FileReadError>> ReadAsync(EditableFile file, int maxBytes = 1024) =>
        Files(_project.Path).ReadAsync(file, maxBytes, CancellationToken.None);

    [Fact]
    public async Task Reads_a_skill_file()
    {
        _project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");

        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md"));

        Assert.Equal("# PDF"u8.ToArray(), read.Value.Head);
        Assert.Equal(5, read.Value.Length);
    }

    [Fact]
    public async Task Reads_a_skill_file_in_a_sub_folder_of_a_codex_skill()
    {
        _project.WriteFile(".agents/skills/pdf/scripts/run.sh", "echo run");

        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.Codex, "pdf", "scripts/run.sh"));

        Assert.Equal("echo run"u8.ToArray(), read.Value.Head);
    }

    [Fact]
    public async Task Reads_at_most_the_limit_plus_one_byte_but_reports_the_real_length()
    {
        _project.WriteFile(".claude/skills/pdf/big.txt", new string('a', 100));

        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "big.txt"), maxBytes: 10);

        Assert.Equal(11, read.Value.Head.Length);
        Assert.Equal(100, read.Value.Length);
    }

    [Fact]
    public async Task A_missing_file_is_missing()
    {
        _project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");

        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "gone.md"));

        Assert.Equal(FileReadError.Missing, read.Error);
    }

    [Fact]
    public async Task A_missing_skill_folder_is_missing()
    {
        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md"));

        Assert.Equal(FileReadError.Missing, read.Error);
    }

    [Fact]
    public async Task A_file_that_is_locked_exclusively_is_unreadable()
    {
        _project.WriteFile(".claude/skills/pdf/SKILL.md", "# PDF");
        var path = Path.Combine(_project.Path, ".claude", "skills", "pdf", "SKILL.md");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var read = await ReadAsync(new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md"));

        Assert.Equal(FileReadError.Unreadable, read.Error);
    }

    [Theory]
    [InlineData("..", "SKILL.md")]
    [InlineData("pdf/other", "SKILL.md")]
    [InlineData("pdf", "../other/SKILL.md")]
    [InlineData("pdf", "scripts/../../other/SKILL.md")]
    public async Task Refuses_a_path_that_leaves_the_skill_folder(string folderName, string relativePath)
    {
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, folderName, relativePath);

        await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(file));
    }

    [Fact]
    public async Task Reads_an_instruction_file_of_a_project()
    {
        _project.WriteFile(".claude/CLAUDE.md", "# Rules");

        var read = await ReadAsync(new EditableFile.InstructionFile(Project, Agent.ClaudeCode, ".claude/CLAUDE.md"));

        Assert.Equal("# Rules"u8.ToArray(), read.Value.Head);
    }

    [Fact]
    public async Task Reads_a_personal_instruction_file_from_the_home_directory()
    {
        _project.WriteFile(".codex/AGENTS.md", "# Mine");

        var read = await ReadAsync(new EditableFile.InstructionFile(new Location.Personal(), Agent.Codex, ".codex/AGENTS.md"));

        Assert.Equal("# Mine"u8.ToArray(), read.Value.Head);
    }

    [Fact]
    public async Task A_missing_instruction_file_is_missing()
    {
        var read = await ReadAsync(new EditableFile.InstructionFile(Project, Agent.Codex, "AGENTS.md"));

        Assert.Equal(FileReadError.Missing, read.Error);
    }

    [Theory]
    [InlineData(Agent.ClaudeCode, "AGENTS.md")]
    [InlineData(Agent.Codex, "CLAUDE.md")]
    [InlineData(Agent.ClaudeCode, "../CLAUDE.md")]
    [InlineData(Agent.ClaudeCode, "notes.md")]
    public async Task Refuses_an_instruction_file_that_is_not_in_the_convention_of_the_agent(Agent agent, string relativePath)
    {
        var file = new EditableFile.InstructionFile(Project, agent, relativePath);

        await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(file));
    }

    [Fact]
    public async Task Refuses_a_personal_path_that_is_only_a_project_convention()
    {
        var file = new EditableFile.InstructionFile(new Location.Personal(), Agent.ClaudeCode, "CLAUDE.md");

        await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(file));
    }
}
