using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

public class ListSkillFilesTests
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly InMemorySkillFiles _files = new();

    [Fact]
    public async Task Puts_the_root_SKILL_md_first_then_the_other_root_files_then_the_folders()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf",
            ("scripts/run.sh", ""), ("notes.md", ""), ("SKILL.md", ""), ("Another.txt", ""));

        var tree = await ListAsync("pdf");

        Assert.Equal(["SKILL.md", "Another.txt", "notes.md", "scripts"], tree.Nodes.Select(NameOf));
    }

    [Fact]
    public async Task Sorts_each_group_ignoring_case_and_keeps_the_names_exactly_as_on_disk()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/p",
            ("SKILL.md", ""), ("b.md", ""), ("A.md", ""), ("References/x.md", ""), ("assets/y.png", ""));

        var tree = await ListAsync("pdf");

        Assert.Equal(["SKILL.md", "A.md", "b.md", "assets", "References"], tree.Nodes.Select(NameOf));
    }

    [Fact]
    public async Task Lists_the_children_of_a_folder_recursively_files_before_folders()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/p",
            ("SKILL.md", ""), ("references/deep/z.md", ""), ("references/b.md", ""), ("references/a.md", ""));

        var tree = await ListAsync("pdf");

        var references = Assert.IsType<SkillFileNode.Folder>(tree.Nodes[1]);
        Assert.Equal(["a.md", "b.md", "deep"], references.Children.Select(NameOf));
        var deep = Assert.IsType<SkillFileNode.Folder>(references.Children[2]);
        var deepFile = Assert.IsType<SkillFileNode.File>(Assert.Single(deep.Children));
        Assert.Equal("references/deep/z.md", deepFile.RelativePath);
    }

    [Fact]
    public async Task A_SKILL_md_below_the_root_is_an_ordinary_file()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/p", ("SKILL.md", ""), ("examples/SKILL.md", ""), ("examples/a.md", ""));

        var tree = await ListAsync("pdf");

        var examples = Assert.IsType<SkillFileNode.Folder>(tree.Nodes[1]);
        Assert.Equal(["a.md", "SKILL.md"], examples.Children.Select(NameOf));
    }

    [Fact]
    public async Task Carries_the_absolute_folder_path_for_Show_in_folder()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", ""));

        var tree = await ListAsync("pdf");

        Assert.Equal("/work/api/.claude/skills/pdf", tree.FolderPath);
    }

    [Fact]
    public async Task Lists_exactly_what_the_port_returns()
    {
        _files.Add(Project, Agent.ClaudeCode, "pdf", "/p", ("SKILL.md", ""), ("a.md", ""));

        var tree = await ListAsync("pdf");

        Assert.Equal(["SKILL.md", "a.md"], tree.Nodes.Select(NameOf));
        Assert.Equal(1, _files.ListCalls);
    }

    [Fact]
    public async Task A_missing_skill_folder_is_reported()
    {
        var result = await new ListSkillFiles(_files).ExecuteAsync(Project, Agent.ClaudeCode, "gone");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillViewError.SkillFolderMissing, result.Error);
    }

    [Fact]
    public async Task A_folder_that_cannot_be_read_is_reported()
    {
        _files.MakeUnlistable(Project, Agent.ClaudeCode, "pdf");

        var result = await new ListSkillFiles(_files).ExecuteAsync(Project, Agent.ClaudeCode, "pdf");

        Assert.Equal(SkillViewError.Unreadable, result.Error);
    }

    private async Task<SkillFileTree> ListAsync(string folderName)
    {
        var result = await new ListSkillFiles(_files).ExecuteAsync(Project, Agent.ClaudeCode, folderName);

        return result.Value;
    }

    private static string NameOf(SkillFileNode node) => node switch
    {
        SkillFileNode.File file => file.Name,
        SkillFileNode.Folder folder => folder.Name,
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };
}
