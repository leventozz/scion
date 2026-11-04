using Bunit;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class SkillPageTests : BunitContext
{
    private const string FolderPath = "/work/api/.claude/skills/pdf";

    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly SkillSummary Pdf = new("pdf", "pdf", "Reads PDFs thoroughly, from text layers to scans.", [], CanBeDeleted: true);

    private readonly UiFakes _fakes = new();

    public SkillPageTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.SkillFiles.Add(Project, Agent.ClaudeCode, "pdf", FolderPath,
            ("SKILL.md", "---\nname: pdf\ndescription: Reads PDFs.\n---\n# PDF skill\n\nBody text.\n"),
            ("scripts/run.sh", "echo one\necho two\n"),
            ("scripts/single.sh", "echo only"),
            ("References/Guide.md", "# Not rendered\n\nplain\n"));
        _fakes.SkillFiles.AddBinary(Project, Agent.ClaudeCode, "pdf", "assets/logo.png", [0x89, 0x50, 0x4E, 0x47, 0, 0, 0]);
    }

    [Fact]
    public void Shows_the_skill_name_the_location_and_agent_and_the_full_description()
    {
        var page = RenderPage();

        Assert.Equal("pdf", page.Find("h1").TextContent);
        Assert.Equal("api · Claude", page.Find(".screen-subtitle").TextContent);
        Assert.Equal("Reads PDFs thoroughly, from text layers to scans.", page.Find(".skill-page-description").TextContent);
    }

    [Fact]
    public void Names_Personal_and_the_agent_in_the_subtitle()
    {
        _fakes.SkillFiles.Add(new Location.Personal(), Agent.Codex, "pdf", "/home/me/.agents/skills/pdf", ("SKILL.md", "# PDF\n"));

        var page = Render<SkillPage>(parameters => parameters
            .Add(page => page.Location, new Location.Personal())
            .Add(page => page.LocationTitle, "Personal")
            .Add(page => page.Agent, Agent.Codex)
            .Add(page => page.Skill, Pdf));

        Assert.Equal("Personal · Codex", page.Find(".screen-subtitle").TextContent);
    }

    [Fact]
    public void Shows_the_warnings_of_the_skill_as_amber_lines()
    {
        var skill = new SkillSummary("pdf", "pdf", "Reads PDFs.", [SkillWarning.NameDoesNotMatchFolder, SkillWarning.DescriptionTooLong], CanBeDeleted: true);

        var page = RenderPage(skill);

        Assert.Equal(
            ["Name doesn't match the folder name \"pdf\".", "Description is longer than 1024 characters."],
            page.FindAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void A_skill_without_a_description_says_so()
    {
        var page = RenderPage(new SkillSummary("pdf", "pdf", "", [SkillWarning.DescriptionMissing], CanBeDeleted: true));

        Assert.Equal("Description unavailable", page.Find(".skill-page-description").TextContent);
    }

    [Fact]
    public void Lists_files_and_folders_exactly_as_on_disk_with_SKILL_md_first_and_selected()
    {
        var page = RenderPage();

        Assert.Equal(
            ["SKILL.md", "assets/", "logo.png", "References/", "Guide.md", "scripts/", "run.sh", "single.sh"],
            page.FindAll(".file-tree-file, .file-tree-folder").Select(entry => entry.TextContent.Trim()));
        Assert.Equal("SKILL.md", Assert.Single(page.FindAll(".file-tree-file[aria-current=true]")).TextContent.Trim());
    }

    [Fact]
    public void Opens_with_the_body_of_SKILL_md_as_markdown_without_the_frontmatter()
    {
        var page = RenderPage();

        Assert.Equal("PDF skill", page.Find(".file-view .markdown h1").TextContent);
        Assert.DoesNotContain("description: Reads PDFs.", page.Find(".file-view").TextContent);
    }

    [Theory]
    [InlineData("---\nname: empty\ndescription: Nothing yet.\n---\n\n")]
    [InlineData("---\nname: empty\ndescription: Nothing yet.\n---\n")]
    [InlineData("---\nname: empty\ndescription: Nothing yet.\n---\n  \n\t\n   ")]
    public void A_SKILL_md_without_a_body_shows_a_calm_empty_state_instead_of_a_blank_area(string skillFile)
    {
        _fakes.SkillFiles.Add(Project, Agent.ClaudeCode, "empty", "/work/api/.claude/skills/empty", ("SKILL.md", skillFile));

        var page = RenderPage(new SkillSummary("empty", "empty", "Nothing yet.", [], CanBeDeleted: true));

        Assert.Equal("This skill has no instructions yet.", page.Find(".file-view .skill-no-instructions").TextContent);
        Assert.Empty(page.FindAll(".file-view .markdown"));
        Assert.Empty(page.FindAll(".file-view .button"));
    }

    [Fact]
    public void A_SKILL_md_with_a_body_has_no_empty_state()
    {
        var page = RenderPage();

        Assert.Empty(page.FindAll(".skill-no-instructions"));
    }

    [Fact]
    public void A_text_file_shows_its_path_the_line_count_and_numbered_lines_without_a_language_label()
    {
        var page = RenderPage();

        OpenFile(page, "run.sh");

        Assert.Equal("scripts/run.sh", page.Find(".text-file-path").TextContent);
        Assert.Equal("2 lines", page.Find(".text-file-count").TextContent);
        Assert.Equal(["1", "2"], page.FindAll(".line-number").Select(number => number.TextContent));
        Assert.Equal(["echo one", "echo two"], page.FindAll(".line-text").Select(line => line.TextContent));
        Assert.Empty(page.FindAll(".markdown"));
        Assert.DoesNotContain("bash", page.Find(".text-file").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void One_line_is_counted_in_the_singular()
    {
        var page = RenderPage();

        OpenFile(page, "single.sh");

        Assert.Equal("1 line", page.Find(".text-file-count").TextContent);
    }

    [Fact]
    public void Selecting_a_file_moves_the_current_marker()
    {
        var page = RenderPage();

        OpenFile(page, "run.sh");

        Assert.Equal("run.sh", Assert.Single(page.FindAll(".file-tree-file[aria-current=true]")).TextContent.Trim());
    }

    [Fact]
    public void A_markdown_file_below_the_root_is_plain_text_with_line_numbers()
    {
        var page = RenderPage();

        OpenFile(page, "Guide.md");

        Assert.Empty(page.FindAll(".markdown"));
        Assert.Equal(["# Not rendered", "", "plain"], page.FindAll(".line-text").Select(line => line.TextContent));
    }

    [Fact]
    public void A_file_that_cannot_be_shown_gets_the_calm_message_and_reveals_its_own_folder()
    {
        var page = RenderPage();

        OpenFile(page, "logo.png");

        Assert.Equal("This file can't be shown here.", page.Find(".file-view .file-not-shown").TextContent);

        page.Find(".file-view .button").Click();

        Assert.Equal([FolderPath + "/assets"], _fakes.Revealer.Shown);
    }

    [Fact]
    public void Show_in_folder_in_the_header_reveals_the_skill_folder()
    {
        var page = RenderPage();

        page.Find(".screen-header-action").Click();

        Assert.Equal([FolderPath], _fakes.Revealer.Shown);
    }

    [Fact]
    public void The_back_arrow_calls_back()
    {
        var backs = 0;
        var page = Render<SkillPage>(parameters => parameters
            .Add(page => page.Location, Project)
            .Add(page => page.LocationTitle, "api")
            .Add(page => page.Agent, Agent.ClaudeCode)
            .Add(page => page.Skill, Pdf)
            .Add(page => page.OnBack, () => backs++));

        page.Find("button[aria-label=Back]").Click();

        Assert.Equal(1, backs);
    }

    [Fact]
    public void A_skill_folder_with_only_SKILL_md_shows_no_file_tree_and_gives_the_reading_area_the_full_width()
    {
        _fakes.SkillFiles.Add(Project, Agent.ClaudeCode, "solo", "/work/api/.claude/skills/solo", ("SKILL.md", "# Solo\n"));

        var page = RenderPage(new SkillSummary("solo", "solo", "Only one file.", [], CanBeDeleted: true));

        Assert.Empty(page.FindAll(".file-tree"));
        Assert.Contains("skill-page-body-single", page.Find(".skill-page-body").ClassList);
        Assert.Equal("Solo", page.Find(".file-view .markdown h1").TextContent);
    }

    [Fact]
    public void A_skill_folder_with_more_files_keeps_the_file_tree_and_two_columns()
    {
        var page = RenderPage();

        Assert.NotNull(page.Find(".file-tree"));
        Assert.DoesNotContain("skill-page-body-single", page.Find(".skill-page-body").ClassList);
    }

    [Fact]
    public void The_description_is_clamped_text_that_can_be_expanded()
    {
        var page = RenderPage();

        Assert.Contains("clamped", page.Find(".skill-page-description").ClassList);
        Assert.NotNull(page.Find(".clamped-text"));
    }

    [Fact]
    public void Uses_the_wide_skill_screen()
    {
        Assert.NotNull(RenderPage().Find("main.screen.skill-screen"));
    }

    [Fact]
    public void A_skill_folder_that_no_longer_exists_says_so_and_keeps_the_back_arrow()
    {
        var page = RenderPage(new SkillSummary("gone", "gone", "Gone.", [], CanBeDeleted: true));

        Assert.Equal("This skill folder no longer exists.", page.Find(".skill-page-message").TextContent);
        Assert.Empty(page.FindAll(".file-tree"));
        Assert.Empty(page.FindAll(".screen-header-action"));
        Assert.NotNull(page.Find("button[aria-label=Back]"));
    }

    [Fact]
    public void A_skill_folder_that_cannot_be_read_says_so()
    {
        _fakes.SkillFiles.MakeUnlistable(Project, Agent.ClaudeCode, "locked");

        var page = RenderPage(new SkillSummary("locked", "locked", "Locked.", [], CanBeDeleted: true));

        Assert.Equal("This skill folder can't be read.", page.Find(".skill-page-message").TextContent);
    }

    private IRenderedComponent<SkillPage> RenderPage(SkillSummary? skill = null) =>
        Render<SkillPage>(parameters => parameters
            .Add(page => page.Location, Project)
            .Add(page => page.LocationTitle, "api")
            .Add(page => page.Agent, Agent.ClaudeCode)
            .Add(page => page.Skill, skill ?? Pdf));

    private static void OpenFile(IRenderedComponent<SkillPage> page, string name) =>
        page.FindAll(".file-tree-file").Single(file => file.TextContent.Trim() == name).Click();
}
