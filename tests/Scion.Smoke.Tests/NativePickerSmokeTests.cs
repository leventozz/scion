using System.Runtime.Versioning;

namespace Scion.Smoke.Tests;

/// <summary>
/// Opens the real native "Add project" dialog. Choosing a folder in it cannot be scripted, so a person
/// checks that by hand; here the dialog is only opened and cancelled.
/// </summary>
[Trait("Category", "Smoke")]
[SupportedOSPlatform("windows")]
public class NativePickerSmokeTests
{
    private const string MainWindowTitle = "Scion";

    [Fact]
    public async Task Add_project_opens_the_native_dialog_and_cancelling_it_changes_nothing()
    {
        await using var app = await DesktopApp.StartAsync();
        var page = app.Page;
        await page.WaitForSelectorAsync(".first-run");
        Assert.Equal([MainWindowTitle], NativeWindows.TitlesOf(app.ProcessId));

        // The dialog is modal, so the click is not awaited until the dialog is gone.
        var click = page.ClickAsync(".button-primary", new() { Timeout = 15000 });
        await Task.Delay(3000);
        var titlesWhileOpen = NativeWindows.TitlesOf(app.ProcessId);
        NativeWindows.CloseAllExceptTitled(app.ProcessId, MainWindowTitle);
        await Task.Delay(1500);
        await click;

        Assert.Contains(titlesWhileOpen, title => title != MainWindowTitle);
        Assert.Equal(1, await page.Locator(".first-run").CountAsync());
        Assert.Equal(0, await page.Locator(".project-card").CountAsync());
        Assert.False(File.Exists(app.RegistryFile));
    }
}
