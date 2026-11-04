using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Read-only viewing: the skill page and the instruction file tab. "Show in folder" and web links go to the
/// recording shell (SCION_TEST_SHELL_LOG), so no Explorer window or browser is ever opened.
/// </summary>
[Trait("Category", "Smoke")]
public class ViewingSmokeTests
{
    private const string SkillMarkdown = """
        ---
        name: docs
        description: Writes API docs.
        ---
        # Writing docs

        Intro with a [link](https://example.com/docs) and `inline`.

        ```bash
        dotnet test --filter Category=Unit
        ```

        | Name | Value |
        |------|-------|
        | a    | 1     |

        <script>window.__pwned = 1</script>

        <img src=x onerror="window.__pwned = 2">
        """;

    private const string HeadingSkill = """
        ---
        name: docs
        description: Writes API docs.
        ---
        # Writing docs

        An opening paragraph in normal text.

        ## When to use

        Use it when the docs are out of date.

        **A bold paragraph that is not a heading.**

        ### Steps

        1. Read the code.
        2. Write what it does.

        ## Instructions

        Keep every page short.
        """;

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Opening_a_skill_shows_its_files_and_renders_the_markdown_body_in_geist_mono_code()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;

        await OpenSkillAsync(page);

        Assert.Equal("docs", (await page.TextContentAsync(".screen-title h1"))!.Trim());
        Assert.Equal("docs-api · Claude", (await page.TextContentAsync(".screen-subtitle"))!.Trim());
        Assert.Equal("Writes API docs.", (await page.TextContentAsync(".skill-page-description"))!.Trim());
        Assert.Equal(
            ["SKILL.md", "assets/", "logo.png", "scripts/", "run.sh"],
            (await page.Locator(".file-tree-file, .file-tree-folder").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal("SKILL.md", (await page.Locator(".file-tree-file[aria-current=true]").TextContentAsync())!.Trim());
        Assert.Equal("Writing docs", (await page.TextContentAsync(".markdown h1"))!.Trim());
        Assert.Equal(["Name", "Value"], (await page.Locator(".markdown th").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.DoesNotContain("description: Writes API docs.", await page.TextContentAsync(".file-view"));
        Assert.Contains("Geist Mono", await ComputedFontFamilyAsync(page, ".markdown pre code"));
        Assert.True(await page.EvaluateAsync<bool>(
            "async () => { await document.fonts.ready; return document.fonts.check('12px \"Geist Mono\"'); }"));
        await app.ScreenshotAsync("skill-page");
    }

    [Fact]
    public async Task Headings_are_three_clearly_different_sizes_larger_than_bold_text_with_more_space_above_than_below()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root, HeadingSkill);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);

        await app.ScreenshotAsync("skill-markdown-headings");
        var h1 = await StyleOfAsync(page, ".markdown h1");
        var h2 = await StyleOfAsync(page, ".markdown h2");
        var h3 = await StyleOfAsync(page, ".markdown h3");
        var bold = await StyleOfAsync(page, ".markdown strong");
        var body = await StyleOfAsync(page, ".markdown p");

        Assert.True(h1.Size > h2.Size && h2.Size > h3.Size, $"H1 {h1.Size}, H2 {h2.Size}, H3 {h3.Size}");
        Assert.True(h3.Size >= bold.Size + 2 && h3.Size >= body.Size + 2, $"H3 {h3.Size}, bold {bold.Size}, body {body.Size}");
        Assert.All(new[] { h2, h3 }, heading => Assert.True(heading.MarginTop > heading.MarginBottom * 2));
        Assert.Equal(0, h1.MarginTop);
        Assert.Equal(["Writing docs", "When to use", "Steps", "Instructions"], (await page.Locator(".markdown h1, .markdown h2, .markdown h3").AllTextContentsAsync()).Select(text => text.Trim()));
    }

    [Fact]
    public async Task A_skill_without_a_body_shows_one_dim_line_in_the_middle_of_the_reading_area()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root, "---\nname: docs\ndescription: Writes API docs.\n---\n\n");
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);

        await app.ScreenshotAsync("skill-no-instructions");
        var area = (await page.Locator(".file-view").BoundingBoxAsync())!;
        var note = (await page.Locator(".skill-no-instructions").BoundingBoxAsync())!;

        Assert.Equal("This skill has no instructions yet.", (await page.TextContentAsync(".skill-no-instructions"))!.Trim());
        Assert.InRange(note.X + note.Width / 2, area.X + area.Width / 2 - 2, area.X + area.Width / 2 + 2);
        Assert.Equal(0, await page.Locator(".file-view .markdown").CountAsync());
    }

    [Fact]
    public async Task A_script_shows_numbered_lines_in_geist_mono_and_a_png_gets_the_calm_message()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);

        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "run.sh" }).ClickAsync();

        await page.WaitForSelectorAsync(".text-file");
        Assert.Equal("scripts/run.sh", (await page.TextContentAsync(".text-file-path"))!.Trim());
        Assert.Equal("3 lines", (await page.TextContentAsync(".text-file-count"))!.Trim());
        Assert.Equal(["1", "2", "3"], (await page.Locator(".line-number").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Contains("Geist Mono", await ComputedFontFamilyAsync(page, ".line-text"));
        await app.ScreenshotAsync("skill-file");

        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "logo.png" }).ClickAsync();

        await page.WaitForSelectorAsync(".file-not-shown");
        Assert.Equal("This file can't be shown here.", (await page.TextContentAsync(".file-not-shown"))!.Trim());
        Assert.Equal(0, await page.Locator(".text-file").CountAsync());
        await app.ScreenshotAsync("skill-binary-file");
    }

    [Fact]
    public async Task Show_in_folder_and_web_links_go_through_the_shell_and_never_navigate_the_view()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        var skillFolder = Path.Combine(project, ".claude", "skills", "docs");
        await using var app = await StartAsync(project, recordShell: true);
        var page = app.Page;
        await OpenSkillAsync(page);
        var urlBefore = page.Url;

        await page.ClickAsync(".screen-header-action");
        await WaitForShellLineAsync(app, $"reveal {skillFolder}");

        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "logo.png" }).ClickAsync();
        await page.WaitForSelectorAsync(".file-not-shown");
        await page.ClickAsync(".file-view .button");
        await WaitForShellLineAsync(app, $"reveal {Path.Combine(skillFolder, "assets")}");

        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "SKILL.md" }).ClickAsync();
        await page.ClickAsync(".markdown a");
        await WaitForShellLineAsync(app, "open https://example.com/docs");

        Assert.Equal(urlBefore, page.Url);
        Assert.Equal(1, await page.Locator(".screen-title h1").CountAsync());
    }

    [Fact]
    public async Task Raw_html_in_a_skill_is_shown_as_text_and_runs_nothing()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);

        Assert.Contains("<script>window.__pwned = 1</script>", await page.TextContentAsync(".markdown"));
        Assert.Equal(0, await page.Locator(".markdown script, .markdown img").CountAsync());
        await Task.Delay(300);
        Assert.True(await page.EvaluateAsync<bool>("() => window.__pwned === undefined"));
    }

    [Fact]
    public async Task The_instruction_tab_shows_both_claude_files_and_codex_has_none()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        WriteFile(Path.Combine(project, "CLAUDE.md"), "# Root rules\n\nBuild with `dotnet build`.\n");
        WriteFile(Path.Combine(project, ".claude", "CLAUDE.md"), "# Nested rules\n");
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);

        await page.Locator("[role=tab]", new PageLocatorOptions { HasText = "CLAUDE.md" }).ClickAsync();

        await page.WaitForSelectorAsync(".instruction-file");
        Assert.Equal(
            ["./CLAUDE.md", "./.claude/CLAUDE.md"],
            (await page.Locator(".instruction-file-path").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal(["Root rules", "Nested rules"], (await page.Locator(".instruction-file h1").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal(0, await page.Locator(".screen-header .button").CountAsync());
        await app.ScreenshotAsync("instruction-file-two-files");

        await page.ClickAsync(".agent-switch button:text-is('Codex')");

        await page.WaitForSelectorAsync(".instruction-empty");
        Assert.Equal("AGENTS.md", (await page.Locator("[role=tab][aria-selected=true]").TextContentAsync())!.Trim());
        Assert.Equal("No AGENTS.md in this project", (await page.TextContentAsync(".instruction-empty"))!.Trim());
        await app.ScreenshotAsync("instruction-file-empty");
    }

    [Fact]
    public async Task Personal_shows_its_own_instruction_file_from_the_personal_root()
    {
        using var scionHome = new TempDirectory("scion-home");
        WriteFile(Path.Combine(scionHome.Path, ".claude", "CLAUDE.md"), "# Personal rules\n");
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;
        await page.WaitForSelectorAsync(".personal-card");
        await page.ClickAsync(".personal-card");
        await page.WaitForSelectorAsync(".screen-title h1");

        await page.Locator("[role=tab]", new PageLocatorOptions { HasText = "CLAUDE.md" }).ClickAsync();

        await page.WaitForSelectorAsync(".instruction-file");
        Assert.Equal("~/.claude/CLAUDE.md", (await page.TextContentAsync(".instruction-file-path"))!.Trim());
        Assert.Equal("Personal rules", (await page.TextContentAsync(".instruction-file h1"))!.Trim());
    }

    [Fact]
    public async Task Going_back_from_a_skill_returns_to_the_skills_tab()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);

        await page.ClickAsync("button[aria-label=Back]");

        await page.WaitForSelectorAsync(".skill-list");
        Assert.Equal("Skills", (await page.Locator("[role=tab][aria-selected=true]").TextContentAsync())!.Trim());
        Assert.Equal("docs-api", (await page.TextContentAsync(".screen-title h1"))!.Trim());
    }

    [Fact]
    public async Task Wide_content_stays_inside_the_reading_area_at_the_window_size_and_at_full_screen()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateLayoutProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page, "sample-skill");

        await AssertLayoutAsync(page, expectWideScreen: false);
        await app.ScreenshotAsync("layout-normal");

        await page.SetViewportSizeAsync(1920, 1080);
        await page.WaitForTimeoutAsync(300);

        await AssertLayoutAsync(page, expectWideScreen: true);
        await app.ScreenshotAsync("layout-fullscreen");
    }

    [Fact]
    public async Task A_long_description_is_cut_at_three_lines_and_can_be_expanded_and_collapsed()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateLayoutProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page, "sample-skill");
        var threeLines = 3 * await LineHeightAsync(page, ".skill-page-description");

        await page.WaitForSelectorAsync(".clamped-text .text-action");
        Assert.Equal("Show more", (await page.TextContentAsync(".clamped-text .text-action"))!.Trim());
        Assert.True(await HeightAsync(page, ".skill-page-description") <= threeLines + 1);

        await page.ClickAsync(".clamped-text .text-action");

        await Assertions.Expect(page.Locator(".clamped-text .text-action")).ToHaveTextAsync("Show less");
        Assert.True(await HeightAsync(page, ".skill-page-description") > threeLines + 1);
        await app.ScreenshotAsync("layout-description-expanded");

        await page.ClickAsync(".clamped-text .text-action");

        await Assertions.Expect(page.Locator(".clamped-text .text-action")).ToHaveTextAsync("Show more");
        Assert.True(await HeightAsync(page, ".skill-page-description") <= threeLines + 1);
    }

    [Fact]
    public async Task A_short_description_has_no_Show_more_and_a_skill_with_only_SKILL_md_has_no_file_tree()
    {
        using var root = new TempDirectory("scion-view");
        var project = CreateLayoutProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;

        await OpenSkillAsync(page, "solo-skill");

        Assert.Equal(0, await page.Locator(".file-tree").CountAsync());
        Assert.Equal(0, await page.Locator(".clamped-text .text-action").CountAsync());
        var body = await WidthAsync(page, ".skill-page-body");
        Assert.True(Math.Abs(await WidthAsync(page, ".file-view") - body) <= 1, "The reading area takes the whole width.");
        await app.ScreenshotAsync("layout-single-file");
    }

    private static string CreateProject(TempDirectory root, string skillMarkdown = SkillMarkdown)
    {
        var project = Path.Combine(root.Path, "docs-api");
        var skill = Path.Combine(project, ".claude", "skills", "docs");
        WriteFile(Path.Combine(skill, "SKILL.md"), skillMarkdown);
        WriteFile(Path.Combine(skill, "scripts", "run.sh"), "#!/bin/sh\necho one\necho two\n");
        Directory.CreateDirectory(Path.Combine(skill, "assets"));
        File.WriteAllBytes(Path.Combine(skill, "assets", "logo.png"), PngHeader);

        return project;
    }

    private static string CreateLayoutProject(TempDirectory root)
    {
        var project = Path.Combine(root.Path, "sample-project");
        var skills = Path.Combine(project, ".claude", "skills");
        var description = string.Join(' ', Enumerable.Repeat(
            "Checks a neutral sample for layout behaviour with a description that is long enough to need about six lines.", 4));
        const string cell = "src/Scion.Infrastructure.FileSystem/Very/Long/Nested/Path/To/Some/File.cs";
        var header = string.Join(" | ", Enumerable.Range(1, 8).Select(index => $"Column {index}"));
        var divider = string.Join(" | ", Enumerable.Repeat("---", 8));
        var row = string.Join(" | ", Enumerable.Range(1, 8).Select(index => $"`{cell}{index}`"));
        var paragraph = string.Join(' ', Enumerable.Repeat("This sentence repeats so that the paragraph is much longer than a readable line.", 6));

        WriteFile(
            Path.Combine(skills, "sample-skill", "SKILL.md"),
            $"---\nname: sample-skill\ndescription: {description}\n---\n# Sample\n\n{paragraph}\n\n| {header} |\n| {divider} |\n| {row} |\n| {row} |\n");
        WriteFile(Path.Combine(skills, "sample-skill", "references", "notes.md"), "# Notes\n");
        WriteFile(
            Path.Combine(skills, "solo-skill", "SKILL.md"),
            "---\nname: solo-skill\ndescription: A short one.\n---\n# Solo\n\nOne file.\n");

        return project;
    }

    private static async Task AssertLayoutAsync(IPage page, bool expectWideScreen)
    {
        Assert.True(
            await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth"),
            "The page must not scroll sideways.");
        var screen = await WidthAsync(page, ".skill-screen");
        Assert.True(screen <= 1200, $"The skill page is at most 1200 px wide, was {screen}.");
        if (expectWideScreen)
        {
            Assert.True(screen > 1100, $"On a wide window the page uses its 1200 px, was {screen}.");
        }

        var table = await page.Locator(".markdown-table-scroll").BoundingBoxAsync();
        var reading = await page.Locator(".file-view").BoundingBoxAsync();
        Assert.NotNull(table);
        Assert.NotNull(reading);
        Assert.True(table.X + table.Width <= reading.X + reading.Width + 1, "The table stays inside the reading area.");
        var paragraph = await WidthAsync(page, ".markdown p");
        Assert.True(paragraph < table.Width || paragraph <= 760, $"Prose keeps a readable line length, was {paragraph}.");
        Assert.DoesNotContain("Newsreader", await ComputedFontFamilyAsync(page, ".markdown h1"));
    }

    private sealed record TextStyle(double Size, double MarginTop, double MarginBottom);

    private static async Task<TextStyle> StyleOfAsync(IPage page, string selector)
    {
        var values = await page.EvaluateAsync<double[]>(
            "selector => { const style = getComputedStyle(document.querySelector(selector)); return [parseFloat(style.fontSize), parseFloat(style.marginTop), parseFloat(style.marginBottom)]; }",
            selector);

        return new TextStyle(values[0], values[1], values[2]);
    }

    private static Task<double> WidthAsync(IPage page, string selector) =>
        page.EvaluateAsync<double>("selector => document.querySelector(selector).getBoundingClientRect().width", selector);

    private static Task<double> HeightAsync(IPage page, string selector) =>
        page.EvaluateAsync<double>("selector => document.querySelector(selector).getBoundingClientRect().height", selector);

    private static Task<double> LineHeightAsync(IPage page, string selector) =>
        page.EvaluateAsync<double>("selector => parseFloat(getComputedStyle(document.querySelector(selector)).lineHeight)", selector);

    private static Task<DesktopApp> StartAsync(string project, bool recordShell = false)
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (project, "docs-api"));

        return DesktopApp.StartAsync(scionHome, recordShell: recordShell);
    }

    private static async Task OpenProjectAsync(IPage page)
    {
        await page.WaitForSelectorAsync(".project-card");
        await page.ClickAsync(".project-open");
        await page.WaitForSelectorAsync(".screen-title h1");
    }

    private static async Task OpenSkillAsync(IPage page, string? skillName = null)
    {
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");
        var row = skillName is null
            ? page.Locator(".skill-row")
            : page.Locator(".skill-row", new PageLocatorOptions { HasText = skillName });
        await row.Locator(".skill-open").ClickAsync();
        await page.WaitForSelectorAsync(".skill-screen .file-view");
        await page.WaitForSelectorAsync(".file-view .markdown, .file-view .skill-no-instructions");
    }

    private static Task<string> ComputedFontFamilyAsync(IPage page, string selector) =>
        page.EvaluateAsync<string>("selector => getComputedStyle(document.querySelector(selector)).fontFamily", selector);

    private static async Task WaitForShellLineAsync(DesktopApp app, string expected)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (app.ReadShellLog().Contains(expected))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Contains(expected, app.ReadShellLog());
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
