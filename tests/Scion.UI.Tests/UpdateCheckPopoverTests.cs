using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Updating;

namespace Scion.UI.Tests;

public class UpdateCheckPopoverTests : BunitContext
{
    private static readonly AvailableUpdate Newer = new("1.1.0", "Faster start.");

    private readonly UiFakes _fakes = new();

    public UpdateCheckPopoverTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<UpdateCheckPopover> RenderOpen()
    {
        var popover = Render<UpdateCheckPopover>();
        popover.Find("button.wordmark").Click();

        return popover;
    }

    private static void Check(IRenderedComponent<UpdateCheckPopover> popover) =>
        popover.Find(".version-action").Click();

    [Fact]
    public void Is_the_wordmark_alone_until_it_is_opened()
    {
        var popover = Render<UpdateCheckPopover>();

        Assert.Equal("Scion", popover.Find("button.wordmark").TextContent);
        Assert.Empty(popover.FindAll(".version-popover"));
    }

    [Fact]
    public void Opens_with_the_version_a_separator_the_check_and_the_start_setting_and_no_status_yet()
    {
        var popover = RenderOpen();

        Assert.Equal("Scion 1.0.0", popover.Find(".version-title").TextContent);
        Assert.Equal("Check for updates", popover.Find(".version-action").TextContent);
        Assert.Equal("Check for updates when Scion starts", popover.Find(".version-setting").TextContent.Trim());
        Assert.NotNull(popover.Find(".version-separator"));
        Assert.Empty(popover.FindAll(".version-status"));
    }

    [Fact]
    public void Says_it_is_up_to_date_after_a_check_that_found_nothing()
    {
        var popover = RenderOpen();

        Check(popover);

        Assert.Equal("You're up to date.", popover.Find(".version-status").TextContent);
        Assert.Equal(1, _fakes.UpdateFeed.Asked);
    }

    [Fact]
    public void Says_it_is_checking_while_the_feed_has_not_answered()
    {
        _fakes.UpdateFeed.Hold = new TaskCompletionSource();
        var popover = RenderOpen();

        Check(popover);

        Assert.Equal("Checking…", popover.Find(".version-status").TextContent);
        _fakes.UpdateFeed.Hold.SetResult();
    }

    [Fact]
    public void Says_updates_are_for_installed_builds_when_scion_is_not_installed()
    {
        _fakes.Installation.IsInstalled = false;
        var popover = RenderOpen();

        Check(popover);

        Assert.Equal("Updates are available in installed builds.", popover.Find(".version-status").TextContent);
        Assert.Equal(0, _fakes.UpdateFeed.Asked);
    }

    [Fact]
    public void A_check_that_fails_says_so_in_dim_amber_with_a_small_try_again_that_checks_again()
    {
        _fakes.UpdateFeed.Failure = new UpdateFailedException("No connection.");
        var popover = RenderOpen();
        Check(popover);

        Assert.Equal("Couldn't check for updates.", popover.Find(".version-status-problem").TextContent);
        _fakes.UpdateFeed.Failure = null;
        popover.Find(".version-status .text-action").Click();

        Assert.Equal("You're up to date.", popover.Find(".version-status").TextContent);
        Assert.Equal(2, _fakes.UpdateFeed.Asked);
    }

    [Fact]
    public void A_check_that_finds_an_update_closes_the_popover_and_opens_the_dialog_about_it()
    {
        _fakes.UpdateFeed.Answer = new UpdateCheck.Available(Newer);
        var popover = RenderOpen();

        Check(popover);

        Assert.Empty(popover.FindAll(".version-popover"));
        Assert.True(_fakes.UpdateSession.IsDialogOpen);
        Assert.Equal(Newer, _fakes.UpdateSession.Update);
    }

    [Fact]
    public void Esc_closes_it()
    {
        var popover = RenderOpen();

        popover.Find(".version-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(popover.FindAll(".version-popover"));
    }

    [Fact]
    public void A_click_outside_closes_it()
    {
        var popover = RenderOpen();

        popover.Find(".menu-backdrop").Click();

        Assert.Empty(popover.FindAll(".version-popover"));
    }

    [Fact]
    public void Clicking_the_wordmark_again_closes_it()
    {
        var popover = RenderOpen();

        popover.Find("button.wordmark").Click();

        Assert.Empty(popover.FindAll(".version-popover"));
    }

    [Fact]
    public void The_start_setting_is_checked_until_the_user_says_otherwise()
    {
        var popover = RenderOpen();

        Assert.True(popover.Find(".version-setting input").HasAttribute("checked"));
    }

    [Fact]
    public void The_start_setting_shows_what_was_saved()
    {
        _fakes.UpdateSettings.Saved = new UpdateSettings(CheckWhenScionStarts: false);

        var popover = RenderOpen();

        Assert.False(popover.Find(".version-setting input").HasAttribute("checked"));
    }

    [Fact]
    public void Changing_the_start_setting_is_saved_at_once()
    {
        var popover = RenderOpen();

        popover.Find(".version-setting input").Change(false);

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: false), _fakes.UpdateSettings.Saved);
    }
}
