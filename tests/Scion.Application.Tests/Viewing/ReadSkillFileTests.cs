using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

public class ReadSkillFileTests
{
    private const string FolderPath = "/work/api/.claude/skills/pdf";

    private static readonly Location Project = new Location.Project("/work/api");

    private readonly InMemorySkillFiles _files = new();

    [Fact]
    public async Task The_root_SKILL_md_is_markdown_without_its_frontmatter()
    {
        Add(("SKILL.md", "---\nname: pdf\ndescription: Reads PDFs.\n---\n# PDF\n"));

        var view = await ReadAsync("SKILL.md");

        Assert.Equal("# PDF\n", Assert.IsType<SkillFileView.Markdown>(view).Body);
    }

    [Fact]
    public async Task A_markdown_file_below_the_root_is_plain_text()
    {
        Add(("references/x.md", "# Heading\ntext\n"));

        var view = await ReadAsync("references/x.md");

        Assert.Equal(["# Heading", "text"], Assert.IsType<SkillFileView.PlainText>(view).Lines);
    }

    [Fact]
    public async Task A_SKILL_md_below_the_root_is_plain_text()
    {
        Add(("examples/SKILL.md", "# Example\n"));

        Assert.IsType<SkillFileView.PlainText>(await ReadAsync("examples/SKILL.md"));
    }

    [Fact]
    public async Task Splits_lines_on_both_line_endings_without_a_trailing_empty_line()
    {
        Add(("scripts/run.sh", "echo a\r\n\r\necho b\n"));

        var view = await ReadAsync("scripts/run.sh");

        Assert.Equal(["echo a", "", "echo b"], Assert.IsType<SkillFileView.PlainText>(view).Lines);
    }

    [Fact]
    public async Task An_empty_file_has_no_lines()
    {
        Add(("notes.txt", ""));

        Assert.Empty(Assert.IsType<SkillFileView.PlainText>(await ReadAsync("notes.txt")).Lines);
    }

    [Fact]
    public async Task A_binary_file_is_not_shown_and_names_its_containing_folder()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", FolderPath, ("assets/logo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0 }));

        var view = await ReadAsync("assets/logo.png");

        Assert.Equal(FolderPath + "/assets", Assert.IsType<SkillFileView.NotShown>(view).FolderPath);
    }

    [Fact]
    public async Task A_file_over_1_MB_is_not_shown()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", FolderPath, ("data.txt", Enumerable.Repeat((byte)'a', FileText.MaxBytes + 1).ToArray()));

        Assert.IsType<SkillFileView.NotShown>(await ReadAsync("data.txt"));
    }

    [Fact]
    public async Task A_file_that_is_not_valid_utf8_is_not_shown()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", FolderPath, ("latin.txt", new byte[] { 0x63, 0x61, 0x66, 0xE9 }));

        Assert.IsType<SkillFileView.NotShown>(await ReadAsync("latin.txt"));
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_not_shown_so_the_user_still_sees_the_calm_message()
    {
        Add(("locked.txt", "secret"));
        _files.MakeUnreadable(Project, Agent.ClaudeCode, "pdf", "locked.txt");

        var view = await ReadAsync("locked.txt");

        Assert.Equal(FolderPath, Assert.IsType<SkillFileView.NotShown>(view).FolderPath);
    }

    [Fact]
    public async Task Asks_the_port_for_at_most_the_size_limit()
    {
        Add(("SKILL.md", "# PDF\n"));

        await ReadAsync("SKILL.md");

        Assert.Equal(FileText.MaxBytes, _files.LastMaxBytes);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("scripts/../../secret.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData(".hidden")]
    [InlineData("")]
    public async Task Rejects_an_unsafe_path_without_calling_the_port(string relativePath)
    {
        Add(("SKILL.md", "# PDF\n"));

        var result = await new ReadSkillFile(_files).ExecuteAsync(Project, Agent.ClaudeCode, "pdf", relativePath);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillViewError.NotInSkillFolder, result.Error);
        Assert.Equal(0, _files.ReadCalls);
    }

    private void Add(params (string Path, string Content)[] files) =>
        _files.Add(Project, Agent.ClaudeCode, "pdf", FolderPath, files);

    private async Task<SkillFileView> ReadAsync(string relativePath)
    {
        var result = await new ReadSkillFile(_files).ExecuteAsync(Project, Agent.ClaudeCode, "pdf", relativePath);

        return result.Value;
    }
}
