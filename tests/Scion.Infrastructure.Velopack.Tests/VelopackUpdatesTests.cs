using Scion.Application.Updating;
using Velopack;

namespace Scion.Infrastructure.Velopack.Tests;

public class VelopackUpdatesTests : IDisposable
{
    private readonly ReleaseFolder _releases = new();
    private readonly TempDirectory _installation = new();

    public void Dispose()
    {
        _releases.Dispose();
        _installation.Dispose();
    }

    private VelopackUpdates InstalledAs(string version) => new(_releases.Path, "9.9.9", Locators.Installed(version, _installation.Path));

    [Fact]
    public async Task A_newer_release_is_available_with_its_notes()
    {
        _releases.Publish("0.1.1", notes: "Faster start.");

        var check = await InstalledAs("0.1.0").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.Available(new AvailableUpdate("0.1.1", "Faster start.")), check);
    }

    [Fact]
    public async Task The_newest_of_several_releases_is_the_one_on_offer()
    {
        _releases.Publish("0.1.1");
        _releases.Publish("0.2.0", notes: "New look.");

        var check = await InstalledAs("0.1.0").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.Available(new AvailableUpdate("0.2.0", "New look.")), check);
    }

    [Fact]
    public async Task The_same_release_is_up_to_date()
    {
        _releases.Publish("0.1.0");

        var check = await InstalledAs("0.1.0").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.UpToDate("0.1.0"), check);
    }

    [Fact]
    public async Task An_older_release_is_not_an_update()
    {
        _releases.Publish("0.1.0");

        var check = await InstalledAs("0.2.0").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.UpToDate("0.2.0"), check);
    }

    [Fact]
    public async Task A_scion_that_is_not_installed_runs_from_source()
    {
        _releases.Publish("0.1.1");
        var updates = new VelopackUpdates(_releases.Path, "9.9.9", Locators.NotInstalled(_installation.Path));

        var check = await updates.CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.RunningFromSource(), check);
    }

    [Fact]
    public async Task Without_a_locator_scion_asks_the_one_found_at_its_start_and_a_scion_run_from_source_is_not_installed()
    {
        VelopackApp.Build().Run();
        _releases.Publish("0.1.1");

        var check = await new VelopackUpdates(_releases.Path, "9.9.9").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.RunningFromSource(), check);
    }

    [Fact]
    public async Task A_feed_that_makes_no_sense_is_a_failed_update()
    {
        _releases.WriteFeed("not json");

        await Assert.ThrowsAsync<UpdateFailedException>(() => InstalledAs("0.1.0").CheckAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_repository_that_cannot_be_reached_is_a_failed_update()
    {
        var updates = new VelopackUpdates("http://127.0.0.1:1/owner/repository", "9.9.9", Locators.Installed("0.1.0", _installation.Path));

        await Assert.ThrowsAsync<UpdateFailedException>(() => updates.CheckAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Downloads_the_package_of_the_update_that_was_found_and_reports_the_progress()
    {
        _releases.Publish("0.1.1", package: [1, 2, 3, 4, 5]);
        var updates = InstalledAs("0.1.0");
        var update = ((UpdateCheck.Available)await updates.CheckAsync(CancellationToken.None)).Update;
        var percent = new List<int>();

        await updates.DownloadAsync(update, new Reported(percent), CancellationToken.None);

        Assert.Equal([1, 2, 3, 4, 5], await File.ReadAllBytesAsync(Path.Combine(_installation.Path, "Scion-0.1.1-full.nupkg")));
        Assert.Equal(100, percent[^1]);
    }

    [Fact]
    public async Task An_update_remembered_from_an_earlier_start_is_looked_up_again_and_downloaded()
    {
        _releases.Publish("0.1.1", package: [1, 2, 3]);

        await InstalledAs("0.1.0").DownloadAsync(new AvailableUpdate("0.1.1", null), new Reported([]), CancellationToken.None);

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_installation.Path, "Scion-0.1.1-full.nupkg")));
    }

    [Fact]
    public async Task An_update_the_feed_no_longer_offers_cannot_be_downloaded()
    {
        _releases.Publish("0.1.2");

        await Assert.ThrowsAsync<UpdateFailedException>(
            () => InstalledAs("0.1.0").DownloadAsync(new AvailableUpdate("0.1.1", null), new Reported([]), CancellationToken.None));
    }

    [Fact]
    public async Task A_feed_that_cannot_be_read_while_looking_up_a_remembered_update_is_a_failed_update()
    {
        _releases.WriteFeed("not json");

        await Assert.ThrowsAsync<UpdateFailedException>(
            () => InstalledAs("0.1.0").DownloadAsync(new AvailableUpdate("0.1.1", null), new Reported([]), CancellationToken.None));
    }

    [Fact]
    public async Task A_package_that_does_not_match_its_checksum_is_a_failed_update()
    {
        _releases.Publish("0.1.1", package: [1, 2, 3]);
        var updates = InstalledAs("0.1.0");
        var update = ((UpdateCheck.Available)await updates.CheckAsync(CancellationToken.None)).Update;
        await File.WriteAllBytesAsync(Path.Combine(_releases.Path, "Scion-0.1.1-full.nupkg"), [9, 9, 9]);

        await Assert.ThrowsAsync<UpdateFailedException>(() => updates.DownloadAsync(update, new Reported([]), CancellationToken.None));
    }

    [Fact]
    public async Task A_cancelled_download_is_cancelled_not_failed()
    {
        _releases.Publish("0.1.1");
        var updates = InstalledAs("0.1.0");
        var update = ((UpdateCheck.Available)await updates.CheckAsync(CancellationToken.None)).Update;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => updates.DownloadAsync(update, new Reported([]), cancellation.Token));
    }

    [Fact]
    public async Task Applying_runs_the_updater_and_restarts_scion_afterwards()
    {
        _releases.Publish("0.1.1");
        var locator = RecordingLocator.In(_releases.Path);
        var updates = new VelopackUpdates(_releases.Path, "9.9.9", locator);
        var update = ((UpdateCheck.Available)await updates.CheckAsync(CancellationToken.None)).Update;

        updates.ApplyWhenScionExits(update);

        var started = Assert.Single(locator.Started);
        Assert.Equal(Path.Combine(_releases.Path, "Update.exe"), started.Program);
        Assert.Equal("apply", started.Arguments[0]);
        Assert.DoesNotContain("--norestart", started.Arguments);
    }

    [Fact]
    public void An_update_that_was_never_found_cannot_be_applied()
    {
        var updates = InstalledAs("0.1.0");

        Assert.Throws<UpdateFailedException>(() => updates.ApplyWhenScionExits(new AvailableUpdate("0.1.1", null)));
    }

    [Fact]
    public async Task An_updater_that_cannot_be_started_is_a_failed_update()
    {
        _releases.Publish("0.1.1");
        var locator = RecordingLocator.In(_releases.Path);
        locator.StartFails = new InvalidOperationException("The updater is gone.");
        var updates = new VelopackUpdates(_releases.Path, "9.9.9", locator);
        var update = ((UpdateCheck.Available)await updates.CheckAsync(CancellationToken.None)).Update;

        var failure = Assert.Throws<UpdateFailedException>(() => updates.ApplyWhenScionExits(update));

        Assert.Equal("The updater is gone.", failure.Message);
    }

    [Fact]
    public void An_installed_scion_has_the_version_it_was_installed_with()
    {
        var installation = (IScionInstallation)InstalledAs("0.1.0");

        Assert.True(installation.IsInstalled);
        Assert.Equal("0.1.0", installation.Version);
    }

    [Fact]
    public void A_scion_that_is_not_installed_has_the_version_of_its_build()
    {
        var installation = (IScionInstallation)new VelopackUpdates(_releases.Path, "1.2.3", Locators.NotInstalled(_installation.Path));

        Assert.False(installation.IsInstalled);
        Assert.Equal("1.2.3", installation.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public async Task Release_notes_that_say_nothing_are_no_notes(string notes)
    {
        _releases.Publish("0.1.1", notes: notes);

        var check = await InstalledAs("0.1.0").CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateCheck.Available(new AvailableUpdate("0.1.1", null)), check);
    }

    /// <summary>Reports synchronously, so a test can read what was reported as soon as the download is done.</summary>
    private sealed class Reported(List<int> percent) : IProgress<int>
    {
        public void Report(int value) => percent.Add(value);
    }
}
