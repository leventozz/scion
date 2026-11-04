using System.Text.Json;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Deleting a skill moves it to Scion's trash, with the real file system. The trash is checked on disk, and the
/// screens of the Slice 11 designs are screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class DeletingSmokeTests
{
    private const string DocsSkill = "---\nname: docs\ndescription: Writes API docs.\n---\n# Writing docs\n";

    [Fact]
    public async Task Delete_moves_the_skill_folder_to_the_trash_with_its_metadata()
    {
        using var root = new TempDirectory("scion-delete");
        var project = CreateProject(root);
        var docs = Path.Combine(project, ".claude", "skills", "docs");
        var docsBytes = await File.ReadAllBytesAsync(Path.Combine(docs, "SKILL.md"));
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");

        await RowOf(page, "docs").Locator(".options-button").ClickAsync();
        await app.ScreenshotAsync("delete-row-menu");
        await page.Locator("[role=menuitem]", new PageLocatorOptions { HasText = "Delete" }).ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");
        await app.ScreenshotAsync("delete-dialog");

        Assert.Equal(["Delete", "Cancel"], (await page.Locator(".dialog-footer .button").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal(["Cancel"], (await page.Locator(".dialog-footer .button-primary").AllTextContentsAsync()).Select(text => text.Trim()));

        await page.Locator(".dialog-footer .button-secondary").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal(["testing"], (await page.Locator(".skill-row .skill-name").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.False(Directory.Exists(docs));
        Assert.True(Directory.Exists(Path.Combine(project, ".claude", "skills", "testing")));

        var trash = Path.Combine(app.ScionHome.Path, ".scion", "trash");
        var entry = Assert.Single(Directory.GetDirectories(trash));
        Assert.Matches(@"^\d{8}T\d{6}Z-docs$", Path.GetFileName(entry));
        Assert.Equal(docsBytes, await File.ReadAllBytesAsync(Path.Combine(entry, "SKILL.md")));

        var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(entry + ".json")).RootElement;
        Assert.Equal(docs, metadata.GetProperty("originalPath").GetString());
        Assert.Equal("project", metadata.GetProperty("location").GetProperty("kind").GetString());
        Assert.Equal(project, metadata.GetProperty("location").GetProperty("path").GetString());
        Assert.Equal("claudeCode", metadata.GetProperty("agent").GetString());
        Assert.True(metadata.TryGetProperty("deletedAt", out _));
        Assert.Equal(2, Directory.GetFileSystemEntries(trash).Length);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Esc")]
    [InlineData("Close")]
    public async Task Backing_out_of_the_dialog_keeps_the_skill_and_creates_no_trash(string way)
    {
        using var root = new TempDirectory("scion-delete");
        var project = CreateProject(root);
        var docs = Path.Combine(project, ".claude", "skills", "docs");
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");
        await RowOf(page, "docs").Locator(".options-button").ClickAsync();
        await page.Locator("[role=menuitem]", new PageLocatorOptions { HasText = "Delete" }).ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        switch (way)
        {
            case "Cancel":
                await page.Locator(".dialog-footer .button-primary").ClickAsync();
                break;
            case "Esc":
                await page.Keyboard.PressAsync("Escape");
                break;
            default:
                await page.Locator("button[aria-label=Close]").ClickAsync();
                break;
        }

        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal(["docs", "testing"], (await page.Locator(".skill-row .skill-name").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal(DocsSkill, await File.ReadAllTextAsync(Path.Combine(docs, "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(app.ScionHome.Path, ".scion", "trash")));
    }

    private static ILocator RowOf(IPage page, string skillName) =>
        page.Locator(".skill-row").Filter(new LocatorFilterOptions { Has = page.Locator(".skill-name", new PageLocatorOptions { HasTextString = skillName }) });

    private static string CreateProject(TempDirectory root)
    {
        var project = Path.Combine(root.Path, "docs-api");
        WriteSkill(project, "docs", DocsSkill);
        WriteSkill(project, "testing", "---\nname: testing\ndescription: Runs the tests.\n---\n# Testing\n");

        return project;
    }

    private static void WriteSkill(string project, string name, string content)
    {
        var skill = Path.Combine(project, ".claude", "skills", name);
        Directory.CreateDirectory(skill);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), content);
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
