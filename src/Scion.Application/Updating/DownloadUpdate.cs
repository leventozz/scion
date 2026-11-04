namespace Scion.Application.Updating;

public sealed class DownloadUpdate(IUpdateDownloader downloader)
{
    public async Task<UpdateDownload> ExecuteAsync(
        AvailableUpdate update, IProgress<int> percent, CancellationToken cancellationToken = default)
    {
        try
        {
            await downloader.DownloadAsync(update, percent, cancellationToken);

            return new UpdateDownload.Downloaded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UpdateDownload.Cancelled();
        }
        catch (UpdateFailedException exception)
        {
            return new UpdateDownload.Failed(exception.Message);
        }
    }
}
