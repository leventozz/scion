using Scion.Application.Updating;

namespace Scion.UI.Tests;

public class UpdateSessionTests
{
    private static readonly AvailableUpdate Newer = new("1.1.0", "Faster start.");

    private readonly FakeUpdateFeed _feed = new();
    private readonly FakeUpdateDownloader _downloader = new();
    private readonly RecordingUpdateApplier _applier = new();
    private readonly RecordingWindowCloser _closer = new();
    private readonly CloseGuard _guard;
    private readonly UpdateSession _session;
    private int _changes;

    public UpdateSessionTests()
    {
        _guard = new CloseGuard(_closer);
        var installation = new FakeScionInstallation();
        var check = new CheckForUpdates(
            _feed, new FakeUpdateSettings(), new FakeUpdateCheckRecord(), installation, new FixedTimeProvider(UiFakes.Noon));
        _session = new UpdateSession(check, new DownloadUpdate(_downloader), new ApplyUpdate(_applier), installation, _guard);
        _session.Changed += () => _changes++;
    }

    private async Task FindAsync()
    {
        _feed.Answer = new UpdateCheck.Available(Newer);
        await _session.StartupCheckAsync();
    }

    [Fact]
    public void Starts_with_nothing_on_offer_and_no_dialog()
    {
        Assert.Equal(UpdateStage.Idle, _session.Stage);
        Assert.Null(_session.Update);
        Assert.False(_session.IsDialogOpen);
        Assert.Equal("1.0.0", _session.CurrentVersion);
    }

    [Fact]
    public async Task The_start_of_scion_puts_a_newer_version_on_offer_without_opening_a_dialog()
    {
        await FindAsync();

        Assert.Equal(UpdateStage.Available, _session.Stage);
        Assert.Equal(Newer, _session.Update);
        Assert.False(_session.IsDialogOpen);
        Assert.True(_changes > 0);
    }

    [Fact]
    public async Task The_start_of_scion_says_nothing_when_it_is_up_to_date()
    {
        await _session.StartupCheckAsync();

        Assert.Equal(UpdateStage.Idle, _session.Stage);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public async Task A_check_at_the_start_of_scion_that_fails_is_never_shown()
    {
        _feed.Failure = new UpdateFailedException("No connection.");

        var exception = await Record.ExceptionAsync(() => _session.StartupCheckAsync());

        Assert.Null(exception);
        Assert.Equal(UpdateStage.Idle, _session.Stage);
    }

    [Fact]
    public async Task A_check_the_user_asks_for_that_finds_an_update_opens_its_dialog()
    {
        _feed.Answer = new UpdateCheck.Available(Newer);

        var result = await _session.ManualCheckAsync();

        Assert.Equal(new UpdateCheck.Available(Newer), result.Value);
        Assert.Equal(UpdateStage.Available, _session.Stage);
        Assert.True(_session.IsDialogOpen);
    }

    [Fact]
    public async Task A_check_the_user_asks_for_that_finds_nothing_changes_nothing()
    {
        var result = await _session.ManualCheckAsync();

        Assert.Equal(new UpdateCheck.UpToDate("1.0.0"), result.Value);
        Assert.Equal(UpdateStage.Idle, _session.Stage);
        Assert.False(_session.IsDialogOpen);
    }

    [Fact]
    public async Task A_check_the_user_asks_for_that_fails_says_so_and_changes_nothing()
    {
        _feed.Failure = new UpdateFailedException("No connection.");

        var result = await _session.ManualCheckAsync();

        Assert.Equal(new UpdateError("No connection."), result.Error);
        Assert.Equal(UpdateStage.Idle, _session.Stage);
    }

    [Fact]
    public async Task A_check_that_finds_the_update_already_downloaded_leaves_it_ready()
    {
        await FindAsync();
        await _session.DownloadAsync();

        await _session.ManualCheckAsync();

        Assert.Equal(UpdateStage.Ready, _session.Stage);
    }

    [Fact]
    public async Task Later_closes_the_dialog_and_keeps_the_update_on_offer()
    {
        await FindAsync();
        _session.OpenDialog();

        _session.Later();

        Assert.False(_session.IsDialogOpen);
        Assert.Equal(UpdateStage.Available, _session.Stage);
    }

    [Fact]
    public void There_is_no_dialog_to_open_without_an_update()
    {
        _session.OpenDialog();

        Assert.False(_session.IsDialogOpen);
    }

    [Fact]
    public async Task Downloads_the_update_on_offer_and_is_then_ready_to_restart()
    {
        await FindAsync();
        _session.OpenDialog();

        await _session.DownloadAsync();

        Assert.Equal([Newer], _downloader.Downloaded);
        Assert.Equal(UpdateStage.Ready, _session.Stage);
        Assert.True(_session.IsDialogOpen);
    }

    [Fact]
    public async Task Shows_how_far_the_download_is_while_it_runs()
    {
        await FindAsync();
        _downloader.Percentages = [10, 42];
        _downloader.Hold = new TaskCompletionSource();

        var downloading = _session.DownloadAsync();

        Assert.Equal(UpdateStage.Downloading, _session.Stage);
        Assert.Equal(42, _session.Percent);
        _downloader.Hold.SetResult();
        await downloading;
    }

    [Fact]
    public async Task A_download_that_fails_is_a_failure_of_the_download()
    {
        await FindAsync();
        _downloader.Failure = new UpdateFailedException("The file is incomplete.");

        await _session.DownloadAsync();

        Assert.Equal(UpdateStage.Failed, _session.Stage);
        Assert.Equal(UpdateFailure.Download, _session.Failure);
    }

    [Fact]
    public async Task Cancelling_the_download_puts_the_update_back_on_offer_and_closes_the_dialog()
    {
        await FindAsync();
        _session.OpenDialog();
        _downloader.Hold = new TaskCompletionSource();
        var downloading = _session.DownloadAsync();

        _session.CancelDownload();
        await downloading;

        Assert.Equal(UpdateStage.Available, _session.Stage);
        Assert.False(_session.IsDialogOpen);
    }

    [Fact]
    public async Task Restarting_asks_about_drafts_arranges_the_installation_and_then_closes_the_window()
    {
        await FindAsync();
        await _session.DownloadAsync();

        await _session.RestartAsync();

        Assert.Equal([Newer], _applier.Applied);
        Assert.Equal(1, _closer.Closed);
    }

    [Fact]
    public async Task Restarting_is_given_up_when_the_user_keeps_editing()
    {
        await FindAsync();
        await _session.DownloadAsync();
        var asked = new TaskCompletionSource<bool>();
        using var tracking = _guard.Track(() => true, () => asked.Task);

        var restarting = _session.RestartAsync();
        asked.SetResult(false);
        await restarting;

        Assert.Empty(_applier.Applied);
        Assert.Equal(0, _closer.Closed);
        Assert.Equal(UpdateStage.Ready, _session.Stage);
    }

    [Fact]
    public async Task Restarting_goes_on_when_the_user_discards_the_draft()
    {
        await FindAsync();
        await _session.DownloadAsync();
        using var tracking = _guard.Track(() => true, () => Task.FromResult(true));

        await _session.RestartAsync();

        Assert.Equal([Newer], _applier.Applied);
        Assert.Equal(1, _closer.Closed);
    }

    [Fact]
    public async Task An_installation_that_cannot_be_arranged_keeps_the_window_open_and_is_a_failure_of_the_installation()
    {
        await FindAsync();
        await _session.DownloadAsync();
        _applier.Failure = new UpdateFailedException("The updater is gone.");

        await _session.RestartAsync();

        Assert.Equal(0, _closer.Closed);
        Assert.Equal(UpdateStage.Failed, _session.Stage);
        Assert.Equal(UpdateFailure.Install, _session.Failure);
    }

    [Fact]
    public async Task Trying_again_after_a_failed_download_downloads_again()
    {
        await FindAsync();
        _downloader.Failure = new UpdateFailedException("The file is incomplete.");
        await _session.DownloadAsync();
        _downloader.Failure = null;

        await _session.TryAgainAsync();

        Assert.Equal(UpdateStage.Ready, _session.Stage);
        Assert.Null(_session.Failure);
    }

    [Fact]
    public async Task Trying_again_after_a_failed_installation_restarts_again()
    {
        await FindAsync();
        await _session.DownloadAsync();
        _applier.Failure = new UpdateFailedException("The updater is gone.");
        await _session.RestartAsync();
        _applier.Failure = null;

        await _session.TryAgainAsync();

        Assert.Equal([Newer], _applier.Applied);
        Assert.Equal(1, _closer.Closed);
    }

    [Fact]
    public async Task Closing_the_dialog_after_a_failure_leaves_the_failure_to_be_opened_again()
    {
        await FindAsync();
        _session.OpenDialog();
        _downloader.Failure = new UpdateFailedException("The file is incomplete.");
        await _session.DownloadAsync();

        _session.Later();
        _session.OpenDialog();

        Assert.True(_session.IsDialogOpen);
        Assert.Equal(UpdateStage.Failed, _session.Stage);
    }
}
