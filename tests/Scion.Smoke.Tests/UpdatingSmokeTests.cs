using System.Text.Json;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Scion updating itself, with the real desktop app, a folder of releases as the update source (never GitHub) and a fake
/// installation (SCION_TEST_INSTALLED_VERSION). The updater that really installs and restarts cannot run in a test:
/// "Restart now" is tried by hand. The screens of the Slice 13 designs are screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class UpdatingSmokeTests
{
    private const string Notes = "- Faster start.\n- Fewer clicks.";

    [Fact]
    public async Task A_newer_release_shows_the_update_action_and_a_dialog_with_its_notes()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1", Notes);
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;

        await page.WaitForSelectorAsync(".update-action");
        await app.ScreenshotAsync("update-entry");
        await page.Locator(".update-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");
        await app.ScreenshotAsync("update-available");

        Assert.Equal("Update to 0.1.1", (await page.Locator(".update-action").TextContentAsync())?.Trim());
        Assert.Equal("Scion 0.1.1 is available", await page.Locator("[role=dialog] h2").TextContentAsync());
        Assert.Equal("You have 0.1.0.", await page.Locator("[role=dialog] .dialog-subtitle").TextContentAsync());
        Assert.Equal(["Faster start.", "Fewer clicks."], await page.Locator(".update-notes li").AllTextContentsAsync());
        Assert.Equal(["Later", "Download update"], await ButtonsAsync(page));
        Assert.Equal(["Download update"], await page.Locator(".dialog-footer .button-primary").AllTextContentsAsync());
    }

    [Fact]
    public async Task A_release_without_notes_says_so()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1");
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;

        await page.WaitForSelectorAsync(".update-action");
        await page.Locator(".update-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        Assert.Equal("No release notes.", await page.Locator(".update-notes-empty").TextContentAsync());
    }

    [Fact]
    public async Task Downloading_ends_with_the_update_ready_and_Later_keeps_the_action()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1", Notes);
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;
        await page.WaitForSelectorAsync(".update-action");
        await page.Locator(".update-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog] h2:has-text('is ready')");
        await app.ScreenshotAsync("update-ready");

        Assert.Equal("Scion 0.1.1 is ready", await page.Locator("[role=dialog] h2").TextContentAsync());
        Assert.Equal("Restart Scion to finish updating.", await page.Locator("[role=dialog] .dialog-subtitle").TextContentAsync());
        Assert.Equal(["Later", "Restart now"], await ButtonsAsync(page));
        Assert.True(File.Exists(Path.Combine(app.ScionHome.Path, ".scion", "test-packages", "Scion-0.1.1-full.nupkg")));

        await page.Locator(".dialog-footer .button-secondary").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal("Update to 0.1.1", (await page.Locator(".update-action").TextContentAsync())?.Trim());
    }

    [Fact]
    public async Task Later_in_the_first_dialog_leaves_the_action_and_the_next_start_the_same_day_shows_it_without_asking_the_feed()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1", Notes);
        string updatesFile;
        await using (var first = await StartAsync(releases, installedVersion: "0.1.0"))
        {
            await first.Page.WaitForSelectorAsync(".update-action");
            await first.Page.Locator(".update-action").ClickAsync();
            await first.Page.WaitForSelectorAsync("[role=dialog]");
            await first.Page.Locator(".dialog-footer .button-secondary").ClickAsync();
            await first.Page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
            Assert.Equal("Update to 0.1.1", (await first.Page.Locator(".update-action").TextContentAsync())?.Trim());
            updatesFile = await File.ReadAllTextAsync(UpdatesFile(first));
        }

        // The source is gone: only what the first start noted can show the update.
        var gone = releases.Path;
        releases.Dispose();
        Assert.False(Directory.Exists(gone));
        await using var second = await StartAsync(gone, installedVersion: "0.1.0", updatesFile: updatesFile);

        await second.Page.WaitForSelectorAsync(".update-action");
        Assert.Equal("Update to 0.1.1", (await second.Page.Locator(".update-action").TextContentAsync())?.Trim());
    }

    [Fact]
    public async Task The_popover_checks_for_updates_and_says_this_is_the_latest_version()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.0");
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;

        await page.Locator("button.wordmark").ClickAsync();
        await page.WaitForSelectorAsync(".version-popover");
        await app.ScreenshotAsync("update-check-closed");
        await page.Locator(".version-action").ClickAsync();
        await page.WaitForSelectorAsync(".version-status");
        await app.ScreenshotAsync("update-check");

        Assert.Equal("Scion 0.1.0", await page.Locator(".version-title").TextContentAsync());
        Assert.Equal("You're up to date.", await page.Locator(".version-status").TextContentAsync());
        Assert.Equal(["Check for updates", "Check for updates when Scion starts"], (await page.Locator(".version-popover").InnerTextAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[2..]);
        Assert.True(await page.Locator(".version-setting input").IsCheckedAsync());
        Assert.True((await page.Locator(".version-status").BoundingBoxAsync())!.Height < 24, "The status line must not wrap.");
        Assert.True((await page.Locator(".version-setting").BoundingBoxAsync())!.Height < 24, "The setting line must not wrap.");
    }

    [Fact]
    public async Task A_check_that_finds_an_update_closes_the_popover_and_opens_the_dialog()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.0");
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;
        await page.Locator("button.wordmark").ClickAsync();
        await page.WaitForSelectorAsync(".version-popover");

        releases.Publish("0.1.1", Notes);
        await page.Locator(".version-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        Assert.Equal("Scion 0.1.1 is available", await page.Locator("[role=dialog] h2").TextContentAsync());
        Assert.Equal(0, await page.Locator(".version-popover").CountAsync());
    }

    [Fact]
    public async Task A_scion_that_is_not_installed_says_updates_are_for_installed_builds()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1");
        await using var app = await StartAsync(releases, installedVersion: null);
        var page = app.Page;

        await page.Locator("button.wordmark").ClickAsync();
        await page.Locator(".version-action").ClickAsync();
        await page.WaitForSelectorAsync(".version-status");

        Assert.Equal("Updates are available in installed builds.", await page.Locator(".version-status").TextContentAsync());
        Assert.Equal(0, await page.Locator(".update-action").CountAsync());
    }

    [Fact]
    public async Task The_start_checkbox_is_written_to_the_settings_file_at_once_and_read_back()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.0");
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;
        await page.Locator("button.wordmark").ClickAsync();
        await page.WaitForSelectorAsync(".version-popover");

        await page.Locator(".version-setting input").UncheckAsync();

        await WaitForAsync(() =>
            ReadSettings(app) is { ValueKind: JsonValueKind.Object } settings
            && settings.TryGetProperty("checkWhenScionStarts", out var value)
            && !value.GetBoolean());
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForSelectorAsync(".version-popover", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        await page.Locator("button.wordmark").ClickAsync();
        await page.WaitForSelectorAsync(".version-popover");
        Assert.False(await page.Locator(".version-setting input").IsCheckedAsync());
    }

    [Fact]
    public async Task A_package_that_does_not_match_its_checksum_is_an_error_dialog_with_a_link_to_the_releases()
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1", Notes);
        await using var app = await StartAsync(releases, installedVersion: "0.1.0", recordShell: true);
        var page = app.Page;
        await page.WaitForSelectorAsync(".update-action");
        await File.WriteAllBytesAsync(releases.PackageOf("0.1.1"), [9, 9, 9]);
        await page.Locator(".update-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog] h2:has-text(\"Couldn't update\")");
        await app.ScreenshotAsync("update-error");

        Assert.Equal("The download failed. Check your connection and try again.", (await page.Locator(".field-problem").TextContentAsync())?.Trim());
        Assert.Equal(["Close", "Try again"], await ButtonsAsync(page));
        Assert.Empty(await page.Locator(".dialog-footer .button-primary").AllTextContentsAsync());

        await page.Locator(".update-link").ClickAsync();

        await WaitForAsync(() => app.ReadShellLog().Length == 1);
        Assert.Matches(@"^open https://.+/releases$", Assert.Single(app.ReadShellLog()));
    }

    [Theory]
    [InlineData("Esc")]
    [InlineData("Close")]
    public async Task Backing_out_of_the_dialog_leaves_the_update_on_offer(string way)
    {
        using var releases = new ReleaseFolder();
        releases.Publish("0.1.1", Notes);
        await using var app = await StartAsync(releases, installedVersion: "0.1.0");
        var page = app.Page;
        await page.WaitForSelectorAsync(".update-action");
        await page.Locator(".update-action").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]");

        if (way == "Esc")
        {
            await page.Keyboard.PressAsync("Escape");
        }
        else
        {
            await page.Locator("button[aria-label=Close]").ClickAsync();
        }

        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("Update to 0.1.1", (await page.Locator(".update-action").TextContentAsync())?.Trim());
    }

    private static Task<DesktopApp> StartAsync(ReleaseFolder releases, string? installedVersion, bool recordShell = false) =>
        StartAsync(releases.Path, installedVersion, recordShell: recordShell);

    /// <summary>Starts the app with one registered project (the update entry lives in the header of the project grid).</summary>
    private static Task<DesktopApp> StartAsync(string releases, string? installedVersion, string? updatesFile = null, bool recordShell = false)
    {
        var scionHome = new TempDirectory("scion-home");
        var parent = new TempDirectory("scion-update-project");
        Fixtures.WriteRegistry(scionHome, (Fixtures.CreateProjectFolder(parent, "api"), "api"));
        if (updatesFile is not null)
        {
            File.WriteAllText(Path.Combine(scionHome.Path, ".scion", "updates.json"), updatesFile);
        }

        return DesktopApp.StartAsync(scionHome, recordShell: recordShell, updateSource: releases, installedVersion: installedVersion);
    }

    private static string UpdatesFile(DesktopApp app) => Path.Combine(app.ScionHome.Path, ".scion", "updates.json");

    private static JsonElement ReadSettings(DesktopApp app)
    {
        try
        {
            using var stream = new FileStream(UpdatesFile(app), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return JsonDocument.Parse(stream).RootElement.Clone();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return default;
        }
    }

    private static async Task<string[]> ButtonsAsync(IPage page) =>
        [.. (await page.Locator(".dialog-footer .button").AllTextContentsAsync()).Select(text => text.Trim())];

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition(), "The condition was not met within 10 seconds.");
    }
}
