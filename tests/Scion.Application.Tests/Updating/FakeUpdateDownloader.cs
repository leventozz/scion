using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class FakeUpdateDownloader : IUpdateDownloader
{
    public IReadOnlyList<int> Percentages { get; set; } = [];

    public UpdateFailedException? Failure { get; set; }

    public AvailableUpdate? Downloaded { get; private set; }

    public Task DownloadAsync(AvailableUpdate update, IProgress<int> percent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var value in Percentages)
        {
            percent.Report(value);
        }

        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Downloaded = update;

        return Task.CompletedTask;
    }
}
