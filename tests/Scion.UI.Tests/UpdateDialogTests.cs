using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Updating;

namespace Scion.UI.Tests;

public class UpdateDialogTests : BunitContext
{
    private static readonly AvailableUpdate Newer = new("1.1.0", "- Faster start.\n- Fewer clicks.");

    private readonly UiFakes _fakes = new();

    public UpdateDialogTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task<IRenderedComponent<UpdateDialog>> OpenAsync(AvailableUpdate? update = null)
    {
        _fakes.UpdateFeed.Answer = new UpdateCheck.Available(update ?? Newer);
        await _fakes.UpdateSession.StartupCheckAsync();
        _fakes.UpdateSession.OpenDialog();

        return Render<UpdateDialog>();
    }

    private static IReadOnlyList<string> Buttons(IRenderedComponent<UpdateDialog> dialog) =>
        [.. dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim())];

    private static void Press(IRenderedComponent<UpdateDialog> dialog, string label) =>
        dialog.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == label).Click();

    private static void Escape(IRenderedComponent<UpdateDialog> dialog) =>
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

    [Fact]
    public void Shows_nothing_while_the_dialog_is_closed()
    {
        _fakes.UpdateSession.OpenDialog();

        Assert.Empty(Render<UpdateDialog>().Markup.Trim());
    }

    [Fact]
    public async Task Offers_the_update_with_what_the_user_has_and_what_is_new_as_rendered_notes()
    {
        var dialog = await OpenAsync();

        Assert.Equal("true", dialog.Find("[role=dialog]").GetAttribute("aria-modal"));
        Assert.Equal("Scion 1.1.0 is available", dialog.Find("h2").TextContent);
        Assert.Equal("You have 1.0.0.", dialog.Find(".dialog-subtitle").TextContent);
        Assert.Equal("What's new", dialog.Find(".update-notes-title").TextContent);
        Assert.Equal(["Faster start.", "Fewer clicks."], dialog.FindAll(".update-notes li").Select(item => item.TextContent));
        Assert.NotNull(dialog.Find("button[aria-label=Close]"));
    }

    [Fact]
    public async Task Says_there_are_no_release_notes_in_dim_text_when_the_release_says_nothing()
    {
        var dialog = await OpenAsync(new AvailableUpdate("1.1.0", null));

        Assert.Equal("No release notes.", dialog.Find(".update-notes-empty").TextContent);
        Assert.Empty(dialog.FindAll(".update-notes .markdown"));
    }

    [Fact]
    public async Task Offers_Later_and_Download_update_with_Download_update_as_the_one_primary_button()
    {
        var dialog = await OpenAsync();

        Assert.Equal(["Later", "Download update"], Buttons(dialog));
        Assert.Equal(["Download update"], dialog.FindAll(".button-primary").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public async Task Puts_the_focus_on_the_dialog_so_Enter_downloads_nothing()
    {
        var dialog = await OpenAsync();

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(dialog.Find("[role=dialog]"));
    }

    [Fact]
    public async Task Later_the_close_button_and_Esc_close_it_and_keep_the_update_on_offer()
    {
        var dialog = await OpenAsync();

        Press(dialog, "Later");
        Assert.False(_fakes.UpdateSession.IsDialogOpen);

        _fakes.UpdateSession.OpenDialog();
        dialog.Find("button[aria-label=Close]").Click();
        Assert.False(_fakes.UpdateSession.IsDialogOpen);

        _fakes.UpdateSession.OpenDialog();
        Escape(dialog);
        Assert.False(_fakes.UpdateSession.IsDialogOpen);
        Assert.Equal(UpdateStage.Available, _fakes.UpdateSession.Stage);
    }

    [Fact]
    public async Task A_click_on_the_backdrop_closes_nothing()
    {
        var dialog = await OpenAsync();

        // The backdrop listens to nothing but keys, so a click on it cannot close the dialog.
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());
        Assert.True(_fakes.UpdateSession.IsDialogOpen);
    }

    [Fact]
    public async Task Download_update_downloads_it_and_then_says_it_is_ready()
    {
        var dialog = await OpenAsync();

        Press(dialog, "Download update");

        Assert.Equal([Newer], _fakes.UpdateDownloader.Downloaded);
        Assert.Equal("Scion 1.1.0 is ready", dialog.Find("h2").TextContent);
        Assert.Equal("Restart Scion to finish updating.", dialog.Find(".dialog-subtitle").TextContent);
        Assert.Equal(["Later", "Restart now"], Buttons(dialog));
        Assert.Equal(["Restart now"], dialog.FindAll(".button-primary").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public async Task Shows_the_download_with_a_bar_and_the_percent_and_a_Cancel_that_stops_it()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Percentages = [42];
        _fakes.UpdateDownloader.Hold = new TaskCompletionSource();

        Press(dialog, "Download update");

        Assert.Equal("Downloading Scion 1.1.0", dialog.Find("h2").TextContent);
        Assert.Equal("42%", dialog.Find(".update-percent").TextContent);
        Assert.Equal("42", dialog.Find("[role=progressbar]").GetAttribute("aria-valuenow"));
        Assert.Contains("width: 42%", dialog.Find(".progress-fill").GetAttribute("style"));
        Assert.Equal(["Cancel"], Buttons(dialog));
        Assert.Empty(dialog.FindAll(".button-primary"));

        Press(dialog, "Cancel");

        dialog.WaitForAssertion(() => Assert.False(_fakes.UpdateSession.IsDialogOpen));
        Assert.Equal(UpdateStage.Available, _fakes.UpdateSession.Stage);
    }

    [Fact]
    public async Task The_close_button_and_Esc_cancel_a_download()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Hold = new TaskCompletionSource();
        Press(dialog, "Download update");

        Escape(dialog);

        dialog.WaitForAssertion(() => Assert.Equal(UpdateStage.Available, _fakes.UpdateSession.Stage));
        Assert.False(_fakes.UpdateSession.IsDialogOpen);
    }

    [Fact]
    public async Task Restart_now_installs_the_update_and_closes_the_window()
    {
        var dialog = await OpenAsync();
        Press(dialog, "Download update");

        Press(dialog, "Restart now");

        Assert.Equal([Newer], _fakes.UpdateApplier.Applied);
        Assert.Equal(1, _fakes.WindowCloser.Closed);
    }

    [Fact]
    public async Task Later_in_the_ready_step_keeps_the_downloaded_update_for_the_next_time()
    {
        var dialog = await OpenAsync();
        Press(dialog, "Download update");

        Press(dialog, "Later");

        Assert.False(_fakes.UpdateSession.IsDialogOpen);
        Assert.Equal(UpdateStage.Ready, _fakes.UpdateSession.Stage);
        Assert.Equal(0, _fakes.WindowCloser.Closed);
    }

    [Fact]
    public async Task A_download_that_fails_says_so_offers_GitHub_and_Try_again()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Failure = new UpdateFailedException("The file is incomplete.");

        Press(dialog, "Download update");

        Assert.Equal("Couldn't update", dialog.Find("h2").TextContent);
        Assert.Equal("The download failed. Check your connection and try again.", dialog.Find(".field-problem").TextContent);
        Assert.NotNull(dialog.Find(".field-problem svg.warning-icon"));
        Assert.Equal("Download from GitHub instead", dialog.Find(".update-link").TextContent);
        Assert.Equal(["Close", "Try again"], Buttons(dialog));
        Assert.Empty(dialog.FindAll(".button-primary"));
    }

    [Fact]
    public async Task Download_from_GitHub_instead_opens_the_releases_page()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Failure = new UpdateFailedException("The file is incomplete.");
        Press(dialog, "Download update");

        dialog.Find(".update-link").Click();

        Assert.Equal([UiFakes.ReleasesPage], _fakes.Links.Opened);
    }

    [Fact]
    public async Task Try_again_downloads_again()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Failure = new UpdateFailedException("The file is incomplete.");
        Press(dialog, "Download update");
        _fakes.UpdateDownloader.Failure = null;

        Press(dialog, "Try again");

        Assert.Equal("Scion 1.1.0 is ready", dialog.Find("h2").TextContent);
    }

    [Fact]
    public async Task Close_after_a_failure_closes_the_dialog_and_leaves_the_update_to_be_opened_again()
    {
        var dialog = await OpenAsync();
        _fakes.UpdateDownloader.Failure = new UpdateFailedException("The file is incomplete.");
        Press(dialog, "Download update");

        Press(dialog, "Close");

        Assert.False(_fakes.UpdateSession.IsDialogOpen);
        Assert.Equal(UpdateStage.Failed, _fakes.UpdateSession.Stage);
    }

    [Fact]
    public async Task An_installation_that_cannot_be_arranged_says_so_and_keeps_the_window_open()
    {
        var dialog = await OpenAsync();
        Press(dialog, "Download update");
        _fakes.UpdateApplier.Failure = new UpdateFailedException("The updater is gone.");

        Press(dialog, "Restart now");

        Assert.Equal("Couldn't update", dialog.Find("h2").TextContent);
        Assert.Equal("Scion couldn't start the update. Try again, or download it from GitHub.", dialog.Find(".field-problem").TextContent);
        Assert.Equal(0, _fakes.WindowCloser.Closed);
    }
}
