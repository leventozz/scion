using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

public class ReadInstructionFilesTests
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly InMemoryInstructionFiles _files = new();

    [Fact]
    public async Task No_file_gives_an_empty_list()
    {
        Assert.Empty(await ReadAsync());
    }

    [Fact]
    public async Task Returns_the_files_in_the_order_of_the_port_with_their_markdown()
    {
        _files.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Root\n"),
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/work/api/.claude", "# Nested\n"));

        var views = await ReadAsync();

        Assert.Equal(["CLAUDE.md", ".claude/CLAUDE.md"], views.Select(view => view.RelativePath));
        Assert.Equal(["/work/api", "/work/api/.claude"], views.Select(view => view.FolderPath));
        Assert.Equal("# Root\n", Assert.IsType<InstructionFileContent.Markdown>(views[0].Content).Text);
    }

    [Fact]
    public async Task Shows_the_whole_file_because_an_instruction_file_has_no_frontmatter_to_drop()
    {
        _files.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "---\nnot: frontmatter\n---\n# Root\n"));

        var views = await ReadAsync();

        Assert.Equal("---\nnot: frontmatter\n---\n# Root\n", Assert.IsType<InstructionFileContent.Markdown>(Assert.Single(views).Content).Text);
    }

    [Fact]
    public async Task A_binary_file_is_not_shown()
    {
        var binary = new InstructionFileBytes("CLAUDE.md", "/work/api", Bytes.Read(new byte[] { 1, 0, 2 }));
        _files.Add(Project, Agent.ClaudeCode, binary);

        Assert.IsType<InstructionFileContent.NotShown>(Assert.Single(await ReadAsync()).Content);
    }

    [Fact]
    public async Task A_file_over_1_MB_is_not_shown()
    {
        var huge = new InstructionFileBytes("CLAUDE.md", "/work/api", Bytes.Read(Enumerable.Repeat((byte)'a', FileText.MaxBytes + 1).ToArray()));
        _files.Add(Project, Agent.ClaudeCode, huge);

        Assert.IsType<InstructionFileContent.NotShown>(Assert.Single(await ReadAsync()).Content);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_not_shown_but_still_listed_with_its_folder()
    {
        _files.Add(Project, Agent.ClaudeCode, new InstructionFileBytes("CLAUDE.md", "/work/api", Bytes.Unreadable));

        var view = Assert.Single(await ReadAsync());

        Assert.IsType<InstructionFileContent.NotShown>(view.Content);
        Assert.Equal("/work/api", view.FolderPath);
    }

    [Fact]
    public async Task Asks_the_port_for_at_most_the_size_limit()
    {
        await ReadAsync();

        Assert.Equal(FileText.MaxBytes, _files.LastMaxBytes);
    }

    private Task<IReadOnlyList<InstructionFileView>> ReadAsync() =>
        new ReadInstructionFiles(_files).ExecuteAsync(Project, Agent.ClaudeCode);
}
