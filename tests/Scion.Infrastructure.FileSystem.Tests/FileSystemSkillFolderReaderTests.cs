using System.Text;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillFolderReaderTests
{
    [Fact]
    public async Task Returns_an_empty_list_when_the_skills_directory_does_not_exist()
    {
        using var project = new TempDirectory();

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Empty(skillFolders);
    }

    [Fact]
    public async Task Reads_the_folder_name_name_and_description_of_a_skill()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/docker-compose/SKILL.md", SkillMarkdown("docker-compose", "Runs compose files."));

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(
            new SkillFolder("docker-compose", "docker-compose", "Runs compose files."),
            Assert.Single(skillFolders));
    }

    [Fact]
    public async Task Each_agent_reads_only_its_own_skills_directory()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/for-claude/SKILL.md", SkillMarkdown("for-claude", "Claude."));
        project.WriteFile(".agents/skills/for-codex/SKILL.md", SkillMarkdown("for-codex", "Codex."));

        var claudeSkills = await ReadSkillsOf(project, Agent.ClaudeCode);
        var codexSkills = await ReadSkillsOf(project, Agent.Codex);

        Assert.Equal(["for-claude"], claudeSkills.Select(skill => skill.FolderName));
        Assert.Equal(["for-codex"], codexSkills.Select(skill => skill.FolderName));
    }

    [Fact]
    public async Task Reads_the_personal_skills_from_the_injected_home_directory()
    {
        using var home = new TempDirectory();
        home.WriteFile(".claude/skills/personal-skill/SKILL.md", SkillMarkdown("personal-skill", "Mine."));

        var skillFolders = await ReaderWithHome(home.Path)
            .ReadAsync(new Location.Personal(), Agent.ClaudeCode, CancellationToken.None);

        Assert.Equal(["personal-skill"], skillFolders.Select(skill => skill.FolderName));
    }

    [Fact]
    public async Task Ignores_folders_without_an_exactly_named_SKILL_md_and_loose_files()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/real-skill/SKILL.md", SkillMarkdown("real-skill", "Real."));
        project.WriteFile(".claude/skills/only-readme/README.md", "# Not a skill\n");
        project.WriteFile(".claude/skills/lowercase-file/skill.md", SkillMarkdown("lowercase-file", "Wrong case."));
        project.WriteFile(".claude/skills/loose-file.md", "# Not a folder\n");

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(["real-skill"], skillFolders.Select(skill => skill.FolderName));
    }

    [Fact]
    public async Task Always_ignores_folders_that_start_with_a_dot()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/real-skill/SKILL.md", SkillMarkdown("real-skill", "Real."));
        project.WriteFile(".claude/skills/.hidden-skill/SKILL.md", SkillMarkdown(".hidden-skill", "Hidden."));
        project.WriteFile(".claude/skills/.scion-tmp/SKILL.md", SkillMarkdown("scratch", "Scratch."));

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(["real-skill"], skillFolders.Select(skill => skill.FolderName));
    }

    [Fact]
    public async Task Reads_null_name_and_description_when_SKILL_md_has_no_frontmatter()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/no-frontmatter/SKILL.md", "# Just a body\nname: not-frontmatter\n");

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(new SkillFolder("no-frontmatter", null, null), Assert.Single(skillFolders));
    }

    [Theory]
    [InlineData("---\nname: [unclosed\ndescription: Fine.\n---\n# Body\n")]
    [InlineData("---\nname: broken\ndescription: Never closed.\n# Body\n")]
    public async Task Flags_a_skill_with_unreadable_frontmatter_instead_of_throwing(string skillMarkdown)
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/broken/SKILL.md", skillMarkdown);

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(
            new SkillFolder("broken", null, null, FrontmatterUnreadable: true),
            Assert.Single(skillFolders));
    }

    [Theory]
    [InlineData("|", "Line one.\nLine two.\n")]
    [InlineData(">", "Line one. Line two.\n")]
    public async Task Reads_multiline_descriptions(string blockIndicator, string expectedDescription)
    {
        using var project = new TempDirectory();
        project.WriteFile(
            ".claude/skills/multiline/SKILL.md",
            $"---\nname: multiline\ndescription: {blockIndicator}\n  Line one.\n  Line two.\n---\n# Body\n");

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(expectedDescription, Assert.Single(skillFolders).Description);
    }

    [Fact]
    public async Task Reads_frontmatter_with_crlf_line_endings_and_a_utf8_byte_order_mark()
    {
        using var project = new TempDirectory();
        project.WriteFile(
            ".claude/skills/windows-file/SKILL.md",
            "---\r\nname: windows-file\r\ndescription: Runs compose files.\r\n---\r\n# Body\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.Equal(
            new SkillFolder("windows-file", "windows-file", "Runs compose files."),
            Assert.Single(skillFolders));
    }

    [Fact]
    public async Task A_plain_skill_folder_is_not_linked()
    {
        using var project = new TempDirectory();
        project.WriteFile(".claude/skills/own/SKILL.md", SkillMarkdown("own", "Lives here."));

        Assert.False(Assert.Single(await ReadSkillsOf(project, Agent.ClaudeCode)).IsLinked);
    }

    [Fact]
    public async Task A_linked_skill_folder_is_marked_as_linked()
    {
        using var shared = new TempDirectory();
        using var project = new TempDirectory();
        shared.WriteFile("shared/SKILL.md", SkillMarkdown("shared", "Lives elsewhere."));
        project.LinkDirectory(".claude/skills/shared", Path.Combine(shared.Path, "shared"));
        project.WriteFile(".claude/skills/own/SKILL.md", SkillMarkdown("own", "Lives here."));

        var skillFolders = await ReadSkillsOf(project, Agent.ClaudeCode);

        Assert.True(skillFolders.Single(skill => skill.FolderName == "shared").IsLinked);
        Assert.False(skillFolders.Single(skill => skill.FolderName == "own").IsLinked);
    }

    [Fact]
    public async Task Skills_inside_a_linked_skills_directory_are_marked_as_linked()
    {
        using var shared = new TempDirectory();
        using var project = new TempDirectory();
        shared.WriteFile("skills/docs/SKILL.md", SkillMarkdown("docs", "Shared."));
        project.LinkDirectory(".claude/skills", Path.Combine(shared.Path, "skills"));

        Assert.True(Assert.Single(await ReadSkillsOf(project, Agent.ClaudeCode)).IsLinked);
    }

    [Fact]
    public async Task Skills_inside_a_linked_agent_folder_are_marked_as_linked()
    {
        using var shared = new TempDirectory();
        using var project = new TempDirectory();
        shared.WriteFile("agent/skills/docs/SKILL.md", SkillMarkdown("docs", "Shared."));
        project.LinkDirectory(".claude", Path.Combine(shared.Path, "agent"));

        Assert.True(Assert.Single(await ReadSkillsOf(project, Agent.ClaudeCode)).IsLinked);
    }

    private static Task<IReadOnlyList<SkillFolder>> ReadSkillsOf(TempDirectory project, Agent agent) =>
        ReaderWithHome(project.Path).ReadAsync(new Location.Project(project.Path), agent, CancellationToken.None);

    private static string SkillMarkdown(string name, string description) =>
        $"---\nname: {name}\ndescription: {description}\n---\n# Body\n";

    private static FileSystemSkillFolderReader ReaderWithHome(string homeDirectory) =>
        new(homeDirectory, [new ClaudeCodeLayout(), new CodexLayout()]);
}
