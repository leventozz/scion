using System.Text;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Creating skills and missing instruction files, with the real file system. The new files are checked on disk byte
/// for byte, and the screens of the Slice 10 designs are screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class CreatingSmokeTests
{
    private const string ExpectedSkill =
        "---\nname: release-notes\ndescription: 'Use when: drafting release notes.'\n---\n\n";

    [Fact]
    public async Task New_skill_from_the_header_writes_the_template_and_opens_it_for_editing()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");

        await app.ScreenshotAsync("create-skills-header");
        await page.Locator(".screen-header .button-primary", new PageLocatorOptions { HasText = "New skill" }).ClickAsync();
        await page.WaitForSelectorAsync("#new-skill-name");
        await app.ScreenshotAsync("create-new-skill");
        await page.Locator("#new-skill-name").FillAsync("release-notes");
        await page.Locator("#new-skill-description").FillAsync("Use when: drafting release notes.");
        await app.ScreenshotAsync("create-new-skill-filled");
        await page.Locator(".dialog .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".text-editor-input");

        var skillFile = Path.Combine(project, ".claude", "skills", "release-notes", "SKILL.md");
        var bytes = await File.ReadAllBytesAsync(skillFile);
        Assert.Equal(new UTF8Encoding(false).GetBytes(ExpectedSkill), bytes);
        Assert.False(Directory.Exists(Path.Combine(project, ".claude", ".scion-tmp")));
        Assert.Equal("release-notes", (await page.TextContentAsync(".skill-page-title h1"))!.Trim());
        Assert.Equal(ExpectedSkill, await page.Locator(".text-editor-input").InputValueAsync());
    }

    [Fact]
    public async Task An_invalid_name_gets_an_amber_line_and_Create_stays_off()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator(".screen-header .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("#new-skill-name");

        await page.Locator("#new-skill-name").FillAsync("Release_Notes");
        await page.Locator("#new-skill-description").FillAsync("Extracts commit messages since the previous tag.");

        Assert.Equal("Use lowercase letters, numbers and hyphens.", (await page.Locator(".field-problem").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog .button-primary").IsDisabledAsync());
        await app.ScreenshotAsync("create-new-skill-invalid-name");
        Assert.False(Directory.Exists(Path.Combine(project, ".claude", "skills", "Release_Notes")));
    }

    [Fact]
    public async Task Enter_in_the_description_does_nothing_and_a_pasted_line_break_becomes_a_space()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator(".screen-header .button-primary").ClickAsync();
        var description = page.Locator("#new-skill-description");

        // The dialog is ready when it has put the focus on Name (the last step of its first render); only then is the
        // description ours to type into. The keys come with no pause at all (Playwright's default): the field must not be
        // written back by the app while they are typed (FieldValue), or keys would be lost.
        await Assertions.Expect(page.Locator("#new-skill-name")).ToBeFocusedAsync();
        await description.ClickAsync();
        await Assertions.Expect(description).ToBeFocusedAsync();
        await description.PressSequentiallyAsync("Reviews");
        await description.PressAsync("Enter");
        await description.PressAsync("Shift+Enter");
        await description.PressSequentiallyAsync("code.");

        await Assertions.Expect(description).ToHaveValueAsync("Reviewscode.");

        await description.FillAsync("");
        await description.EvaluateAsync("element => { element.focus(); document.execCommand('insertText', false, 'one\\ntwo'); }");

        await Assertions.Expect(description).ToHaveValueAsync("one two");
    }

    [Fact]
    public async Task A_name_that_exists_is_refused_and_the_existing_skill_is_untouched()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        var existing = Path.Combine(project, ".claude", "skills", "docs", "SKILL.md");
        var before = await File.ReadAllBytesAsync(existing);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator(".screen-header .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("#new-skill-name");

        await page.Locator("#new-skill-name").FillAsync("docs");
        await page.Locator("#new-skill-description").FillAsync("Validate compose environments before deployment.");

        Assert.Equal("A skill with this name already exists here.", (await page.Locator(".field-problem").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog .button-primary").IsDisabledAsync());
        await app.ScreenshotAsync("create-new-skill-exists");
        Assert.Equal(before, await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task New_skill_in_an_empty_location_is_the_secondary_button_next_to_Copy_from()
    {
        using var root = new TempDirectory("scion-create");
        var project = Path.Combine(root.Path, "docs-api");
        Directory.CreateDirectory(project);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);

        await page.WaitForSelectorAsync(".empty-state");
        await app.ScreenshotAsync("create-empty-state");
        await page.Locator(".empty-state .button-secondary", new PageLocatorOptions { HasText = "New skill" }).ClickAsync();
        await page.Locator("#new-skill-name").FillAsync("first-skill");
        await page.Locator("#new-skill-description").FillAsync("The first one.");
        await page.Locator(".dialog .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".text-editor-input");

        Assert.True(File.Exists(Path.Combine(project, ".claude", "skills", "first-skill", "SKILL.md")));
    }

    [Fact]
    public async Task Create_CLAUDE_md_writes_the_project_title_and_opens_the_editor()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator("[role=tab]").Nth(1).ClickAsync();
        await page.WaitForSelectorAsync(".instruction-empty-state");
        await app.ScreenshotAsync("create-instruction-file-create");

        await page.Locator(".instruction-empty-state .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".text-editor-input");

        Assert.Equal(Encoding.UTF8.GetBytes("# docs-api\n\n"), await File.ReadAllBytesAsync(Path.Combine(project, "CLAUDE.md")));
        Assert.Equal("# docs-api\n\n", await page.Locator(".text-editor-input").InputValueAsync());
        Assert.DoesNotContain(Directory.GetFileSystemEntries(project), entry => Path.GetFileName(entry).Contains(".scion-"));
    }

    [Fact]
    public async Task Create_AGENTS_md_for_Personal_makes_the_codex_folder_in_the_home_directory()
    {
        using var root = new TempDirectory("scion-create");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await page.WaitForSelectorAsync(".personal-card");
        await page.ClickAsync(".personal-card");
        await page.WaitForSelectorAsync(".screen-title h1");
        await page.Locator(".agent-switch button", new PageLocatorOptions { HasText = "Codex" }).ClickAsync();
        await page.Locator("[role=tab]").Nth(1).ClickAsync();
        await page.WaitForSelectorAsync(".instruction-empty-state");

        await page.Locator(".instruction-empty-state .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".text-editor-input");

        Assert.Equal("", await File.ReadAllTextAsync(Path.Combine(app.ScionHome.Path, ".codex", "AGENTS.md")));
        Assert.Equal("~/.codex/AGENTS.md", (await page.TextContentAsync(".instruction-file-path"))!.Trim());
    }

    private static string CreateProject(TempDirectory root)
    {
        var project = Path.Combine(root.Path, "docs-api");
        var skill = Path.Combine(project, ".claude", "skills", "docs");
        Directory.CreateDirectory(skill);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), "---\nname: docs\ndescription: Writes API docs.\n---\n# Writing docs\n");

        return project;
    }

    private static Task<DesktopApp> StartAsync(string project)
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (project, "docs-api"));

        return DesktopApp.StartAsync(scionHome);
    }

    private static async Task OpenProjectAsync(IPage page)
    {
        await page.WaitForSelectorAsync(".project-card");
        await page.ClickAsync(".project-open");
        await page.WaitForSelectorAsync(".screen-title h1");
    }
}
