using System.Text;
using Scion.Application.Editing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Editing;

public class OpenFileForEditingTests
{
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly EditableFile SkillMd = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md");

    private readonly InMemoryEditableFiles _files = new();

    [Fact]
    public async Task Opens_a_text_file_for_editing()
    {
        _files.Put(SkillMd, "# PDF\nBody\n");

        var result = await OpenAsync(SkillMd);

        Assert.Equal("# PDF\nBody\n", result.Value.Text);
    }

    [Fact]
    public async Task Gives_the_text_LF_line_breaks_so_a_draft_can_be_compared_with_a_plain_string_comparison()
    {
        _files.Put(SkillMd, "a\r\nb\rc\n");

        var result = await OpenAsync(SkillMd);

        Assert.Equal("a\nb\nc\n", result.Value.Text);
    }

    [Fact]
    public async Task Strips_the_BOM_from_the_text_and_remembers_it_in_the_format()
    {
        _files.Put(SkillMd, (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("a\r\nb")]);

        var result = await OpenAsync(SkillMd);

        Assert.Equal("a\nb", result.Value.Text);
        Assert.Equal(new TextFormat(true, LineEnding.CrLf), result.Value.Baseline.Format);
    }

    [Fact]
    public async Task The_baseline_hash_is_the_hash_of_the_bytes_on_disk()
    {
        var bytes = (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("a\r\nb")];
        _files.Put(SkillMd, bytes);

        var result = await OpenAsync(SkillMd);

        Assert.Equal(ContentHash.Of(bytes), result.Value.Baseline.Hash);
    }

    [Fact]
    public async Task Opens_an_instruction_file()
    {
        var claudeMd = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, "CLAUDE.md");
        _files.Put(claudeMd, "# Rules\n");

        var result = await OpenAsync(claudeMd);

        Assert.Equal("# Rules\n", result.Value.Text);
    }

    [Fact]
    public async Task A_missing_file_is_reported_as_missing()
    {
        var result = await OpenAsync(SkillMd);

        Assert.Equal(OpenForEditingError.Missing, result.Error);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_reported_as_unreadable()
    {
        _files.Put(SkillMd, "locked");
        _files.MakeUnreadable(SkillMd);

        var result = await OpenAsync(SkillMd);

        Assert.Equal(OpenForEditingError.Unreadable, result.Error);
    }

    [Fact]
    public async Task A_binary_file_is_not_editable()
    {
        _files.Put(SkillMd, (byte[])[0x89, 0x50, 0x00, 0x47]);

        Assert.Equal(OpenForEditingError.NotEditable, (await OpenAsync(SkillMd)).Error);
    }

    [Fact]
    public async Task A_file_over_1_MB_is_not_editable()
    {
        _files.Put(SkillMd, Enumerable.Repeat((byte)'a', FileText.MaxBytes + 1).ToArray());

        Assert.Equal(OpenForEditingError.NotEditable, (await OpenAsync(SkillMd)).Error);
    }

    [Fact]
    public async Task A_file_that_is_not_valid_utf8_is_not_editable()
    {
        _files.Put(SkillMd, (byte[])[0x63, 0x61, 0x66, 0xE9]);

        Assert.Equal(OpenForEditingError.NotEditable, (await OpenAsync(SkillMd)).Error);
    }

    [Fact]
    public async Task Asks_the_port_for_at_most_the_size_limit()
    {
        _files.Put(SkillMd, "x");

        await OpenAsync(SkillMd);

        Assert.Equal(FileText.MaxBytes, _files.LastMaxBytes);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData(".hidden")]
    [InlineData("")]
    public async Task Rejects_an_unsafe_skill_path_without_calling_the_port(string relativePath)
    {
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", relativePath);

        var result = await OpenAsync(file);

        Assert.Equal(OpenForEditingError.NotInSkillFolder, result.Error);
        Assert.Equal(0, _files.ReadCalls);
    }

    private Task<Result<EditableText, OpenForEditingError>> OpenAsync(EditableFile file) =>
        new OpenFileForEditing(_files).ExecuteAsync(file);
}
