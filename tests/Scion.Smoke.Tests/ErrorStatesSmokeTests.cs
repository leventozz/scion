namespace Scion.Smoke.Tests;

[Trait("Category", "Smoke")]
public class ErrorStatesSmokeTests
{
    private const string CorruptRegistry = "{ this is corrupt";

    [Fact]
    public async Task An_unreadable_registry_shows_the_error_screen_and_leaves_the_file_untouched()
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistryText(scionHome, CorruptRegistry);
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;

        await page.WaitForSelectorAsync(".error-screen");

        Assert.Equal("Scion couldn't read your project list", await page.TextContentAsync("h1"));
        Assert.EndsWith("projects.json", await page.TextContentAsync(".error-path"));
        Assert.Equal("Your old file is kept as a backup.", await page.TextContentAsync(".error-note"));
        Assert.Equal(1, await page.Locator(".button-primary").CountAsync());
        Assert.Equal(CorruptRegistry, File.ReadAllText(app.RegistryFile));
        Assert.Equal(["projects.json"], Directory.GetFiles(Path.GetDirectoryName(app.RegistryFile)!).Select(Path.GetFileName));
        await app.ScreenshotAsync("registry-unreadable");
    }

    [Fact]
    public async Task Start_fresh_keeps_the_old_file_as_a_backup_and_shows_the_first_run_entry()
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistryText(scionHome, CorruptRegistry);
        var bytesBefore = File.ReadAllBytes(Path.Combine(scionHome.Path, ".scion", "projects.json"));
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;
        await page.WaitForSelectorAsync(".error-screen");

        await page.ClickAsync(".button-primary");

        await page.WaitForSelectorAsync(".first-run");
        var backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(app.RegistryFile)!, "projects.json.corrupt-*"));
        Assert.Equal(bytesBefore, File.ReadAllBytes(backup));
        Assert.False(File.Exists(app.RegistryFile));
    }

    [Fact]
    public async Task A_project_whose_folder_is_gone_says_so_and_can_be_removed_from_its_screen()
    {
        using var projects = new TempDirectory("scion-proj");
        var missingFolder = Path.Combine(projects.Path, "gone-project");
        var keptFolder = Fixtures.CreateProjectFolder(projects, "stay-here");
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (missingFolder, "gone-project"), (keptFolder, "stay-here"));
        await using var app = await DesktopApp.StartAsync(scionHome);
        var page = app.Page;
        await page.WaitForSelectorAsync(".project-card");

        var missingCard = page.Locator(".project-card", new() { HasText = "gone-project" });
        Assert.Equal("Folder not found", (await missingCard.Locator(".folder-missing").TextContentAsync())!.Trim());
        Assert.Equal(0, await missingCard.Locator(".skill-counts").CountAsync());
        Assert.Equal(1, await page.Locator(".project-card .skill-counts").CountAsync());
        Assert.Equal("rgb(245, 158, 11)", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('.folder-missing')).color"));
        await app.ScreenshotAsync("grid-folder-missing");

        await missingCard.Locator(".project-open").ClickAsync();
        await page.WaitForSelectorAsync(".folder-missing-state");

        Assert.Equal("This folder no longer exists.", await page.TextContentAsync(".folder-missing-state p"));
        Assert.Equal(0, await page.Locator(".agent-switch").CountAsync());
        await app.ScreenshotAsync("location-folder-missing");

        await page.ClickAsync(".folder-missing-state .button-secondary");

        await page.WaitForSelectorAsync(".project-grid");
        Assert.Equal(["stay-here"], await page.Locator(".project-name").AllTextContentsAsync());
        Assert.Single(Fixtures.ReadRegisteredProjects(app));
    }
}
