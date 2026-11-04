using Bunit;
using Scion.Application.Updating;

namespace Scion.UI.Tests;

public class UpdateEntryTests : BunitContext
{
    private static readonly AvailableUpdate Newer = new("1.1.0", "Faster start.");

    private readonly UiFakes _fakes = new();

    public UpdateEntryTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task PutOnOfferAsync()
    {
        _fakes.UpdateFeed.Answer = new UpdateCheck.Available(Newer);
        await _fakes.UpdateSession.StartupCheckAsync();
    }

    [Fact]
    public void Shows_nothing_while_no_update_is_on_offer()
    {
        var entry = Render<UpdateEntry>();

        Assert.Empty(entry.Markup.Trim());
    }

    [Fact]
    public async Task Offers_the_update_as_a_mint_text_action_with_its_version()
    {
        await PutOnOfferAsync();

        var entry = Render<UpdateEntry>();

        var action = entry.Find("button.text-action");
        Assert.Equal("Update to 1.1.0", action.TextContent);
        Assert.Empty(entry.FindAll(".button"));
    }

    [Fact]
    public async Task Appears_when_an_update_is_found_after_it_was_first_shown()
    {
        var entry = Render<UpdateEntry>();

        await PutOnOfferAsync();

        entry.WaitForAssertion(() => Assert.Equal("Update to 1.1.0", entry.Find("button.text-action").TextContent));
    }

    [Theory]
    [InlineData(UpdateStage.Available)]
    [InlineData(UpdateStage.Ready)]
    [InlineData(UpdateStage.Failed)]
    public async Task Stays_while_the_update_is_on_offer_downloaded_or_failed(UpdateStage stage)
    {
        await PutOnOfferAsync();
        switch (stage)
        {
            case UpdateStage.Ready:
                await _fakes.UpdateSession.DownloadAsync();
                break;
            case UpdateStage.Failed:
                _fakes.UpdateDownloader.Failure = new UpdateFailedException("The file is incomplete.");
                await _fakes.UpdateSession.DownloadAsync();
                break;
        }

        var entry = Render<UpdateEntry>();

        Assert.Equal(stage, _fakes.UpdateSession.Stage);
        Assert.Equal("Update to 1.1.0", entry.Find("button.text-action").TextContent);
    }

    [Fact]
    public async Task A_click_opens_the_dialog_about_the_update()
    {
        await PutOnOfferAsync();
        var entry = Render<UpdateEntry>();

        entry.Find("button.text-action").Click();

        Assert.True(_fakes.UpdateSession.IsDialogOpen);
    }
}
