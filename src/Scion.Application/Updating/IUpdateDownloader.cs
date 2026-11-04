namespace Scion.Application.Updating;

public interface IUpdateDownloader
{
    /// <param name="percent">Receives how much of the download is done, 0 to 100.</param>
    /// <exception cref="UpdateFailedException">The download failed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was cancelled.</exception>
    Task DownloadAsync(AvailableUpdate update, IProgress<int> percent, CancellationToken cancellationToken);
}
