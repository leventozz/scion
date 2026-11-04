using System.Text.Json;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

[Trait("Category", "Smoke")]
public class ProjectGridSmokeTests
{
    [Fact]
    public async Task First_run_shows_the_dictionary_entry_with_one_primary_button()
    {
        await using var app = await DesktopApp.StartAsync();
        var page = app.Page;

        await page.WaitForSelectorAsync(".first-run");

        Assert.Equal("scion", await page.TextContentAsync(".first-run h1"));
        Assert.Equal(1, await page.Locator(".button-primary").CountAsync());
        Assert.Equal(0, await page.Locator(".button-secondary, .project-card").CountAsync());
        Assert.Matches(@"^Claude \d+ · Codex \d+$", await page.TextContentAsync(".personal-card .skill-counts") ?? "");
        await app.ScreenshotAsync("first-run");
    }

    [Fact]
    public async Task Adding_a_project_opens_it_and_the_grid_shows_it_with_the_design_palette_and_tilde_paths()
    {
        using var projects = new TempDirectory("scion-proj");
        var projectFolder = Fixtures.CreateProjectFolder(projects, "demo-project");
        await using var app = await DesktopApp.StartAsync(testFolder: projectFolder);
        var page = app.Page;
        await page.WaitForSelectorAsync(".first-run");

        await page.ClickAsync(".button-primary");
        await page.WaitForSelectorAsync(".skill-row");

        Assert.Equal("demo-project", await page.TextContentAsync("h1"));
        Assert.Equal(ExpectedDisplayPath(projectFolder), await page.TextContentAsync(".screen-path"));
        Assert.Equal(["x"], await page.Locator(".skill-name").AllTextContentsAsync());
        Assert.Equal("DP", await page.TextContentAsync(".avatar"));
        await app.ScreenshotAsync("project-screen");

        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".project-card");

        Assert.Equal(1, await page.Locator(".project-card").CountAsync());
        Assert.True(await page.EvaluateAsync<bool>(
            "() => !!(document.querySelector('.personal-card').compareDocumentPosition(document.querySelector('.project-card')) & Node.DOCUMENT_POSITION_FOLLOWING)"));
        Assert.Equal("Claude 1 · Codex 0", await page.TextContentAsync(".project-card .skill-counts"));
        Assert.True(await page.EvaluateAsync<bool>(
            "() => parseFloat(getComputedStyle(document.querySelector('.project-card .count-zero')).opacity) < 0.6"));
        Assert.Equal(ExpectedDisplayPath(projectFolder), await page.TextContentAsync(".project-card .project-path"));
        Assert.Equal("demo-project", await page.TextContentAsync(".project-card .path-tail"));
        Assert.Contains("Newsreader", await ComputedStyleAsync(page, ".project-name", "fontFamily"));
        Assert.DoesNotContain("Locations", await page.ContentAsync());

        Assert.Contains("avatar-clay", (await page.GetAttributeAsync(".project-card .avatar", "class"))!);
        Assert.Equal("rgb(58, 41, 36)", await ComputedStyleAsync(page, ".project-card .avatar", "backgroundColor"));
        Assert.Equal("rgb(195, 146, 132)", await ComputedStyleAsync(page, ".project-card .avatar", "color"));

        var tildeColor = await page.EvaluateAsync<int[]>(
            @"() => getComputedStyle(document.querySelector('.personal-home')).color.match(/\d+/g).map(Number).slice(0, 3)");
        Assert.True(tildeColor.Max() - tildeColor.Min() <= 12, $"The ~ must be gray, but is rgb({string.Join(", ", tildeColor)}).");

        Assert.Equal(1, await page.Locator(".button-secondary").CountAsync());
        Assert.Equal(1, await page.Locator(".button-secondary svg.add-icon").CountAsync());
        Assert.Equal(0, await page.Locator(".button-primary").CountAsync());
        Assert.Equal("rgb(42, 42, 44)", await ComputedStyleAsync(page, ".button-secondary", "backgroundColor"));
        await app.ScreenshotAsync("grid");
    }

    [Fact]
    public async Task Adding_the_same_folder_again_keeps_one_card_and_the_registry_stays_valid()
    {
        using var projects = new TempDirectory("scion-proj");
        var projectFolder = Fixtures.CreateProjectFolder(projects, "demo-project");
        await using var app = await DesktopApp.StartAsync(testFolder: projectFolder);
        var page = app.Page;
        await page.WaitForSelectorAsync(".first-run");
        await page.ClickAsync(".button-primary");
        await page.WaitForSelectorAsync(".skill-row");
        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".project-card");

        await page.ClickAsync(".button-secondary");
        await page.WaitForSelectorAsync(".skill-row");
        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".project-card");

        Assert.Equal(1, await page.Locator(".project-card").CountAsync());
        await page.ClickAsync(".project-open");
        await page.WaitForSelectorAsync("h1");
        Assert.Equal("demo-project", await page.TextContentAsync("h1"));
        await page.ClickAsync("button[aria-label=Back]");
        await page.ClickAsync(".personal-card");
        await page.WaitForSelectorAsync("h1");
        Assert.Equal("Personal", await page.TextContentAsync("h1"));

        Assert.Single(Fixtures.ReadRegisteredProjects(app));
        Assert.Equal(["projects.json"], Directory.GetFiles(Path.GetDirectoryName(app.RegistryFile)!).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Remove_from_scion_unregisters_the_project_and_leaves_its_folder_on_disk()
    {
        using var projects = new TempDirectory("scion-proj");
        var removed = Fixtures.CreateProjectFolder(projects, "keep-me");
        var other = Fixtures.CreateProjectFolder(projects, "stay-here");
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (removed, "keep-me"), (other, "stay-here"));
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;
        await page.WaitForSelectorAsync(".project-card");

        await OpenMenuOfAsync(page, "keep-me");
        await app.ScreenshotAsync("card-menu");
        await page.Locator("[role=menuitem]", new() { HasText = "Remove from Scion" }).ClickAsync();

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.project-card').length === 1");
        Assert.Equal(["stay-here"], await page.Locator(".project-name").AllTextContentsAsync());
        Assert.Equal(
            [other],
            Fixtures.ReadRegisteredProjects(app).Select(project => project.GetProperty("path").GetString()));
        Assert.True(File.Exists(Path.Combine(removed, ".claude", "skills", "x", "SKILL.md")));
    }

    [Fact]
    public async Task Rename_selects_the_name_and_enter_saves_it_into_the_registry()
    {
        using var projects = new TempDirectory("scion-proj");
        var projectFolder = Fixtures.CreateProjectFolder(projects, "rename-me");
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (projectFolder, "rename-me"));
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;
        await page.WaitForSelectorAsync(".project-card");

        await OpenMenuOfAsync(page, "rename-me");
        await page.Locator("[role=menuitem]", new() { HasText = "Rename" }).ClickAsync();
        await page.WaitForSelectorAsync("input.project-name-input");

        Assert.True(await page.EvaluateAsync<bool>(
            "() => { const input = document.querySelector('input.project-name-input'); return document.activeElement === input && input.selectionStart === 0 && input.selectionEnd === input.value.length; }"));
        await app.ScreenshotAsync("rename");

        await page.FillAsync("input.project-name-input", "Renamed Project");
        await page.PressAsync("input.project-name-input", "Enter");

        await page.WaitForSelectorAsync(".project-name");
        Assert.Equal("Renamed Project", await page.TextContentAsync(".project-name"));
        var saved = Assert.Single(Fixtures.ReadRegisteredProjects(app));
        Assert.Equal("Renamed Project", saved.GetProperty("displayName").GetString());
        Assert.Equal(projectFolder, saved.GetProperty("path").GetString());
    }

    private static async Task OpenMenuOfAsync(IPage page, string projectName)
    {
        var card = page.Locator(".project-card", new() { HasText = projectName });
        await card.Locator(".options-button").ClickAsync();
        await page.WaitForSelectorAsync("[role=menu]");
    }

    private static Task<string> ComputedStyleAsync(IPage page, string selector, string property) =>
        page.EvaluateAsync<string>($"() => getComputedStyle(document.querySelector('{selector}')).{property}");

    /// <summary>The screens show the home directory as "~", the way the user thinks about it.</summary>
    private static string ExpectedDisplayPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;
    }
}
