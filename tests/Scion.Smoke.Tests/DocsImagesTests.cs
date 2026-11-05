using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Makes the screenshots of the real app that the README and the website show (<c>docs/images/</c>). They are not checks: run
/// them on request with <c>dotnet test tests/Scion.Smoke.Tests --filter Category=DocsImages</c> and look at the PNGs.
/// They use the same neutral sample world as the other images (storefront-web, billing-api, docs-site), in temporary folders.
/// </summary>
[Trait("Category", "DocsImages")]
public class DocsImagesTests
{
    private const int Width = 1280;
    private const int Height = 800;

    private const string WriteTestsDescription = "Writes unit tests for a module with Jest, following the project's naming and mocking conventions.";

    private const string WriteTestsBody =
        "# Write tests\n\n"
        + "Write unit tests for the module the user names.\n\n"
        + "1. Read the module and list what it does.\n"
        + "2. Name each test after a behaviour, not after a function.\n"
        + "3. Mock only at the edges: the network and the clock.\n"
        + "4. Keep each test to one idea.\n";

    [Fact]
    public async Task Skill_edit()
    {
        using var projects = new TempDirectory("scion-docs-images");
        var storefront = CreateStorefrontWeb(projects);
        await using var app = await StartAsync(projects, storefront);
        var page = app.Page;
        await page.SetViewportSizeAsync(Width, Height);
        await OpenProjectAsync(page, "storefront-web");

        await page.Locator(".skill-row", new PageLocatorOptions { HasText = "write-tests" }).Locator(".skill-open").ClickAsync();
        await page.WaitForSelectorAsync(".file-view .markdown");
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea");
        await page.Locator("textarea").FillAsync(
            $"---\nname: write-tests\ndescription: {WriteTestsDescription}\n---\n{WriteTestsBody}5. Run the suite and report what failed.\n");
        await page.WaitForSelectorAsync(".unsaved-changes");

        await app.CaptureAsync(Path.Combine(ImagesFolder(), "skill-edit.png"));
    }

    [Fact]
    public async Task Copy_conflict()
    {
        using var projects = new TempDirectory("scion-docs-images");
        var storefront = CreateStorefrontWeb(projects);
        var billing = Path.Combine(projects.Path, "billing-api");
        Fixtures.WriteClaudeSkill(
            billing, "release-notes", "Drafts a changelog entry for every merged pull request.", "# Release notes\n\nOne bullet per pull request.\n");
        await using var app = await StartAsync(projects, storefront);
        var page = app.Page;
        await page.SetViewportSizeAsync(Width, Height);
        await OpenProjectAsync(page, "storefront-web");

        await page.Locator(".skill-row", new PageLocatorOptions { HasText = "release-notes" }).Locator(".options-button").ClickAsync();
        await page.ClickAsync("[role=menuitem]:text-is('Copy to…')");
        await page.WaitForSelectorAsync(".dialog");
        await page.ClickAsync(".place-picker-button");
        await page.Locator(".place-option", new PageLocatorOptions { HasText = "billing-api" }).ClickAsync();
        await page.WaitForSelectorAsync(".conflict-choice");

        // Only the dialog: the screen behind it shows where the temporary folders are.
        await app.CaptureAsync(Path.Combine(ImagesFolder(), "copy-conflict.png"), ".dialog");
    }

    /// <summary>The project of the other images: four skills, with the descriptions the README already shows.</summary>
    private static string CreateStorefrontWeb(TempDirectory projects)
    {
        var storefront = Path.Combine(projects.Path, "storefront-web");
        Fixtures.WriteClaudeSkill(storefront, "accessibility-review", "Reviews a component for keyboard access, focus order and colour contrast.");
        Fixtures.WriteClaudeSkill(
            storefront, "api-conventions", "Explains how storefront-web calls the backend, with its paths, error shapes and retry rules.");
        Fixtures.WriteClaudeSkill(storefront, "release-notes", "Drafts release notes from merged pull requests, grouped by feature, fix and chore.");
        Fixtures.WriteClaudeSkill(storefront, "write-tests", WriteTestsDescription, WriteTestsBody);

        return storefront;
    }

    private static async Task<DesktopApp> StartAsync(TempDirectory projects, string storefront)
    {
        var billing = Path.Combine(projects.Path, "billing-api");
        var docs = Path.Combine(projects.Path, "docs-site");
        Directory.CreateDirectory(billing);
        Directory.CreateDirectory(docs);

        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (storefront, "storefront-web"), (billing, "billing-api"), (docs, "docs-site"));

        return await DesktopApp.StartAsync(scionHome);
    }

    private static async Task OpenProjectAsync(IPage page, string name)
    {
        await page.WaitForSelectorAsync(".project-card");
        await page.Locator(".project-card", new PageLocatorOptions { HasText = name }).Locator(".project-open").ClickAsync();
        await page.WaitForSelectorAsync(".skill-row");
    }

    private static string ImagesFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scion.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("scion.slnx not found."), "docs", "images");
    }
}
