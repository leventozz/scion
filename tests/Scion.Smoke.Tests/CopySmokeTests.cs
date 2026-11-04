using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Copy scenarios between two temporary projects and Personal. The app runs with SCION_HOME set, so Personal is
/// a temporary folder too and the real ~/.claude/skills is never touched.
/// </summary>
[Trait("Category", "Smoke")]
public class CopySmokeTests
{
    private static readonly byte[] BinaryContent = [0x00, 0xFF, 0x0D, 0x0A, 0x80, 0x1A, 0x00];

    [Fact]
    public async Task Pulling_into_an_empty_project_copies_the_whole_skill_folder_byte_for_byte()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha skill.", withExtras: true);
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-b");

        await page.WaitForSelectorAsync(".empty-state");
        Assert.Equal("Copy from…", (await page.TextContentAsync(".empty-state .button-primary"))!.Trim());
        Assert.Equal(0, await page.Locator(".screen-header .button").CountAsync());
        await app.ScreenshotAsync("copy-empty-state");

        await page.ClickAsync(".empty-state .button-primary");
        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("proj-b · Claude", (await page.TextContentAsync(".copy-fixed"))!.Trim());
        Assert.Equal("Copy", (await page.TextContentAsync(".dialog .button-primary"))!.Trim());
        Assert.True(await page.Locator(".dialog .button-primary").IsDisabledAsync());
        Assert.Contains("Choose a project to copy from.", await page.TextContentAsync(".copy-empty"));
        await app.ScreenshotAsync("copy-dialog-choose-source");

        await ChoosePlaceAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".copy-row");
        await page.ClickAsync(".text-action:text-is('Select all')");
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("1 skill will be copied to proj-b.");
        await app.ScreenshotAsync("copy-dialog-pull");
        await page.ClickAsync(".dialog .button-primary");

        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        await page.WaitForSelectorAsync(".skill-row");
        Assert.Equal(["alpha"], await page.Locator(".skill-name").AllTextContentsAsync());
        Assert.Equal(TreeOf(SkillFolder(a, "alpha")), TreeOf(SkillFolder(b, "alpha")));
        AssertNoHelperFolders(b);
    }

    [Fact]
    public async Task Pushing_a_skill_over_a_different_one_and_choosing_overwrite_replaces_it_without_leftovers()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha, new version.", withExtras: true);
        WriteSkill(b, "alpha", "Alpha, old version.", withExtras: false);
        File.WriteAllText(Path.Combine(SkillFolder(b, "alpha"), "old-only.txt"), "old");
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".skill-row");
        await app.ScreenshotAsync("copy-entry");

        await page.ClickAsync(".skill-row .options-button");
        await app.ScreenshotAsync("copy-row-menu");
        await page.ClickAsync("[role=menuitem]:text-is('Copy to…')");
        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("proj-a · Claude", (await page.TextContentAsync(".copy-fixed"))!.Trim());
        await ChoosePlaceAsync(page, "proj-b");

        await page.WaitForSelectorAsync(".conflict-choice");
        Assert.Equal("Already in proj-b, different content", await page.TextContentAsync(".copy-row .skill-warning"));
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("0 skills will be copied to proj-b, 1 skipped.");
        await app.ScreenshotAsync("copy-dialog-conflict");

        await page.ClickAsync(".conflict-choice button:text-is('Overwrite')");
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("1 skill will be copied to proj-b, 1 overwritten.");
        await page.ClickAsync(".dialog .button-primary");

        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal(TreeOf(SkillFolder(a, "alpha")), TreeOf(SkillFolder(b, "alpha")));
        AssertNoHelperFolders(b);
    }

    [Fact]
    public async Task Skipping_a_conflict_leaves_the_target_alone_while_the_other_skill_is_copied()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha, new version.", withExtras: false);
        WriteSkill(a, "beta", "Beta skill.", withExtras: false);
        WriteSkill(b, "alpha", "Alpha, old version.", withExtras: false);
        var targetBefore = TreeOf(SkillFolder(b, "alpha"));
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-b");
        await page.WaitForSelectorAsync(".skill-row");

        await page.ClickAsync(".screen-header .button-secondary");
        await page.WaitForSelectorAsync(".dialog");
        await ChoosePlaceAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".copy-row");

        await page.ClickAsync(".text-action:text-is('Select all')");

        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("1 skill will be copied to proj-b, 1 skipped.");
        await page.ClickAsync(".dialog .button-primary");
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal(targetBefore, TreeOf(SkillFolder(b, "alpha")));
        Assert.Equal(TreeOf(SkillFolder(a, "beta")), TreeOf(SkillFolder(b, "beta")));
        AssertNoHelperFolders(b);
    }

    [Fact]
    public async Task A_skill_whose_content_is_identical_in_the_target_is_marked_and_needs_no_choice()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha skill.", withExtras: true);
        WriteSkill(b, "alpha", "Alpha skill.", withExtras: true);
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".skill-row");
        await page.ClickAsync(".skill-row .options-button");
        await page.ClickAsync("[role=menuitem]:text-is('Copy to…')");
        await page.WaitForSelectorAsync(".dialog");

        await ChoosePlaceAsync(page, "proj-b");

        await page.WaitForSelectorAsync(".skill-note");
        Assert.Equal("Identical in proj-b", await page.TextContentAsync(".skill-note"));
        Assert.Equal(0, await page.Locator(".conflict-choice").CountAsync());
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("0 skills will be copied to proj-b, 1 already identical.");
        Assert.True(await page.Locator(".dialog .button-primary").IsDisabledAsync());
        Assert.Equal("rgb(138, 147, 139)", await ComputedColorAsync(page, ".skill-note"));
        await app.ScreenshotAsync("copy-dialog-identical");
    }

    [Fact]
    public async Task Pulling_a_personal_skill_into_a_project_copies_it_byte_for_byte()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        await using var app = await StartWithProjectsAsync(a, b, personalRoot => WriteSkill(personalRoot, "gamma", "Gamma skill.", withExtras: true));
        var page = app.Page;
        await OpenProjectAsync(page, "proj-b");
        await page.WaitForSelectorAsync(".empty-state");

        await page.ClickAsync(".empty-state .button-primary");
        await page.WaitForSelectorAsync(".dialog");
        await ChoosePlaceAsync(page, "Personal");
        await page.WaitForSelectorAsync(".copy-row");
        await page.ClickAsync(".text-action:text-is('Select all')");
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("1 skill will be copied to proj-b.");
        await app.ScreenshotAsync("copy-dialog-personal-source");
        await page.ClickAsync(".dialog .button-primary");

        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        await page.WaitForSelectorAsync(".skill-row");
        Assert.Equal(TreeOf(SkillFolder(app.ScionHome.Path, "gamma")), TreeOf(SkillFolder(b, "gamma")));
        AssertNoScratch(b);
    }

    [Fact]
    public async Task Pushing_a_project_skill_to_personal_lands_under_scion_home_and_nowhere_else()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha skill.", withExtras: true);
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".skill-row");

        await page.ClickAsync(".skill-row .options-button");
        await page.ClickAsync("[role=menuitem]:text-is('Copy to…')");
        await page.WaitForSelectorAsync(".dialog");
        await ChoosePlaceAsync(page, "Personal");
        await Assertions.Expect(page.Locator(".copy-summary")).ToHaveTextAsync("1 skill will be copied to Personal.");
        await page.ClickAsync(".dialog .button-primary");

        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal(TreeOf(SkillFolder(a, "alpha")), TreeOf(SkillFolder(app.ScionHome.Path, "alpha")));
        AssertNoScratch(app.ScionHome.Path);

        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".personal-card");
        Assert.Equal("Claude 1 · Codex 0", await page.TextContentAsync(".personal-card .skill-counts"));
    }

    [Fact]
    public async Task Starting_the_app_clears_scratch_leftovers_and_puts_back_a_parked_skill()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        var guid = "0123456789abcdef0123456789abcdef";
        WriteFile(Path.Combine(a, ".claude", ".scion-tmp", $"tmp-half-{guid}", "SKILL.md"), "half a copy\n");
        WriteFile(
            Path.Combine(a, ".claude", ".scion-tmp", $"old-alpha-{guid}", "SKILL.md"),
            "---\nname: alpha\ndescription: Parked.\n---\n");
        var personalScratch = "";
        await using var app = await StartWithProjectsAsync(a, b, personalRoot =>
        {
            personalScratch = Path.Combine(personalRoot, ".agents", ".scion-tmp");
            WriteFile(Path.Combine(personalScratch, $"tmp-half-{guid}", "SKILL.md"), "half a copy\n");
        });

        await WaitUntilAsync(() =>
            !Directory.Exists(Path.Combine(a, ".claude", ".scion-tmp")) && !Directory.Exists(personalScratch));

        Assert.True(File.Exists(Path.Combine(SkillFolder(a, "alpha"), "SKILL.md")), "The parked skill must be back in place.");
        Assert.False(Directory.Exists(SkillFolder(a, "half")));
    }

    [Fact]
    public async Task Escape_closes_the_dialog_and_a_click_on_the_backdrop_does_not()
    {
        using var projects = new TempDirectory("scion-copy");
        var (a, b) = CreateProjects(projects);
        WriteSkill(a, "alpha", "Alpha skill.", withExtras: false);
        await using var app = await StartWithProjectsAsync(a, b);
        var page = app.Page;
        await OpenProjectAsync(page, "proj-a");
        await page.WaitForSelectorAsync(".skill-row");
        await page.ClickAsync(".screen-header .button-secondary");
        await page.WaitForSelectorAsync(".dialog");

        await page.Locator(".dialog-backdrop").ClickAsync(new LocatorClickOptions { Position = new Position { X = 5, Y = 5 } });
        Assert.Equal(1, await page.Locator(".dialog").CountAsync());

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
    }

    private static (string A, string B) CreateProjects(TempDirectory projects)
    {
        var a = Path.Combine(projects.Path, "proj-a");
        var b = Path.Combine(projects.Path, "proj-b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        return (a, b);
    }

    /// <param name="seedPersonal">Gets the folder that is Personal's root (SCION_HOME) before the app starts.</param>
    private static async Task<DesktopApp> StartWithProjectsAsync(string a, string b, Action<string>? seedPersonal = null)
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (a, "proj-a"), (b, "proj-b"));
        seedPersonal?.Invoke(scionHome.Path);

        return await DesktopApp.StartAsync(scionHome);
    }

    private static string SkillFolder(string project, string name) => Path.Combine(project, ".claude", "skills", name);

    private static void WriteSkill(string project, string name, string description, bool withExtras)
    {
        var folder = SkillFolder(project, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), $"---\nname: {name}\ndescription: {description}\n---\nBody\n");

        if (withExtras)
        {
            Directory.CreateDirectory(Path.Combine(folder, "scripts"));
            File.WriteAllText(Path.Combine(folder, "scripts", "run.sh"), "echo run\n");
            File.WriteAllBytes(Path.Combine(folder, "logo.bin"), BinaryContent);
        }
    }

    private static async Task OpenProjectAsync(IPage page, string name)
    {
        await page.WaitForSelectorAsync(".project-card");
        await page.Locator(".project-card", new PageLocatorOptions { HasText = name }).Locator(".project-open").ClickAsync();
        await page.WaitForSelectorAsync(".screen-title h1");
    }

    private static async Task ChoosePlaceAsync(IPage page, string name)
    {
        await page.ClickAsync(".place-picker-button");
        await page.Locator(".place-option", new PageLocatorOptions { HasText = name }).ClickAsync();
    }

    private static Task<string> ComputedColorAsync(IPage page, string selector) =>
        page.EvaluateAsync<string>("selector => getComputedStyle(document.querySelector(selector)).color", selector);

    /// <summary>Every file below the folder: relative path to bytes as hex.</summary>
    private static Dictionary<string, string> TreeOf(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(folder, file).Replace('\\', '/'),
            file => Convert.ToHexString(File.ReadAllBytes(file)));

    private static void AssertNoHelperFolders(string root)
    {
        var dotEntries = Directory.EnumerateFileSystemEntries(Path.Combine(root, ".claude", "skills"))
            .Select(Path.GetFileName)
            .Where(name => name!.StartsWith('.'));

        Assert.Empty(dotEntries);
        AssertNoScratch(root);
    }

    private static void AssertNoScratch(string root) =>
        Assert.False(Directory.Exists(Path.Combine(root, ".claude", ".scion-tmp")), "The scratch directory must be gone.");

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition(), "The condition did not become true within 10 seconds.");
    }
}
