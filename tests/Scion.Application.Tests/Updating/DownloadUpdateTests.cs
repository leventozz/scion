using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class DownloadUpdateTests
{
    private static readonly AvailableUpdate NewerVersion = new("0.2.0", null);

    private readonly FakeUpdateDownloader _downloader = new();
    private readonly List<int> _reported = [];

    private Task<UpdateDownload> DownloadAsync(CancellationToken cancellationToken = default) =>
        new DownloadUpdate(_downloader).ExecuteAsync(NewerVersion, new Progress(_reported), cancellationToken);

    [Fact]
    public async Task Downloads_the_update_it_was_given()
    {
        var download = await DownloadAsync();

        Assert.Equal(new UpdateDownload.Downloaded(), download);
        Assert.Equal(NewerVersion, _downloader.Downloaded);
    }

    [Fact]
    public async Task Reports_how_far_the_download_is()
    {
        _downloader.Percentages = [10, 55, 100];

        await DownloadAsync();

        Assert.Equal([10, 55, 100], _reported);
    }

    [Fact]
    public async Task A_cancelled_download_is_cancelled_not_failed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var download = await DownloadAsync(cancellation.Token);

        Assert.Equal(new UpdateDownload.Cancelled(), download);
        Assert.Null(_downloader.Downloaded);
    }

    [Fact]
    public async Task A_download_that_fails_says_why()
    {
        _downloader.Failure = new UpdateFailedException("The file is incomplete.");

        var download = await DownloadAsync();

        Assert.Equal(new UpdateDownload.Failed("The file is incomplete."), download);
    }

    /// <summary>Reports synchronously, so a test can read what was reported as soon as the download is done.</summary>
    private sealed class Progress(List<int> reported) : IProgress<int>
    {
        public void Report(int value) => reported.Add(value);
    }
}
