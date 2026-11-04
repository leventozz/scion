using Bunit;
using Microsoft.JSInterop;

namespace Scion.UI.Tests;

public class SaveShortcutTests : BunitContext
{
    private int _saved;

    public SaveShortcutTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Listens_for_the_shortcut_only_while_active()
    {
        RenderShortcut(active: false);

        Assert.Empty(JSInterop.Invocations["scion.onSaveShortcut"]);
    }

    [Fact]
    public void Starts_listening_when_it_becomes_active()
    {
        var shortcut = RenderShortcut(active: false);

        shortcut.Render(parameters => parameters.Add(component => component.Active, true));

        var invocation = Assert.Single(JSInterop.Invocations["scion.onSaveShortcut"]);
        Assert.IsType<DotNetObjectReference<SaveShortcut>>(invocation.Arguments[0]);
    }

    [Fact]
    public void Does_not_listen_twice_when_it_renders_again_while_active()
    {
        var shortcut = RenderShortcut(active: true);

        shortcut.Render(parameters => parameters.Add(component => component.Active, true));

        Assert.Single(JSInterop.Invocations["scion.onSaveShortcut"]);
    }

    [Fact]
    public async Task Calls_back_when_the_browser_reports_the_shortcut()
    {
        var shortcut = RenderShortcut(active: true);

        await shortcut.InvokeAsync(() => shortcut.Instance.ReportShortcut());

        Assert.Equal(1, _saved);
    }

    [Fact]
    public void Stops_listening_when_it_becomes_inactive()
    {
        var shortcut = RenderShortcut(active: true);

        shortcut.Render(parameters => parameters.Add(component => component.Active, false));

        Assert.Single(JSInterop.Invocations["scion.offSaveShortcut"]);
    }

    [Fact]
    public async Task Stops_listening_when_it_goes_away_while_active()
    {
        var shortcut = RenderShortcut(active: true);

        await shortcut.Instance.DisposeAsync();

        Assert.Single(JSInterop.Invocations["scion.offSaveShortcut"]);
    }

    [Fact]
    public async Task Does_not_stop_listening_when_it_goes_away_while_inactive()
    {
        var shortcut = RenderShortcut(active: false);

        await shortcut.Instance.DisposeAsync();

        Assert.Empty(JSInterop.Invocations["scion.offSaveShortcut"]);
    }

    private IRenderedComponent<SaveShortcut> RenderShortcut(bool active) =>
        Render<SaveShortcut>(parameters => parameters
            .Add(shortcut => shortcut.Active, active)
            .Add(shortcut => shortcut.OnSave, () => _saved++));
}
