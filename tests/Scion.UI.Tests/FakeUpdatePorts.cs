using Scion.Application.Updating;

namespace Scion.UI.Tests;

internal sealed class FakeScionInstallation : IScionInstallation
{
    public bool IsInstalled { get; set; } = true;

    public string Version { get; set; } = "1.0.0";
}

internal sealed class FakeUpdateFeed : IUpdateFeed
{
    public UpdateCheck Answer { get; set; } = new UpdateCheck.UpToDate("1.0.0");

    public UpdateFailedException? Failure { get; set; }

    /// <summary>When set, the check waits until the test completes it.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public int Asked { get; private set; }

    public async Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken)
    {
        Asked++;
        if (Hold is not null)
        {
            await Hold.Task;
        }

        return Failure is null ? Answer : throw Failure;
    }
}

internal sealed class FakeUpdateDownloader : IUpdateDownloader
{
    public IReadOnlyList<int> Percentages { get; set; } = [];

    public UpdateFailedException? Failure { get; set; }

    /// <summary>When set, the download reports its percentages and then waits until the test completes it or cancels.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public List<AvailableUpdate> Downloaded { get; } = [];

    public async Task DownloadAsync(AvailableUpdate update, IProgress<int> percent, CancellationToken cancellationToken)
    {
        foreach (var value in Percentages)
        {
            percent.Report(value);
        }

        if (Hold is not null)
        {
            await Hold.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }

        Downloaded.Add(update);
    }
}

internal sealed class RecordingUpdateApplier : IUpdateApplier
{
    public List<AvailableUpdate> Applied { get; } = [];

    public UpdateFailedException? Failure { get; set; }

    public void ApplyWhenScionExits(AvailableUpdate update)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Applied.Add(update);
    }
}

internal sealed class FakeUpdateSettings : IUpdateSettings
{
    public UpdateSettings Saved { get; set; } = new(CheckWhenScionStarts: true);

    public Task<UpdateSettings> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

    public Task WriteAsync(UpdateSettings settings, CancellationToken cancellationToken)
    {
        Saved = settings;

        return Task.CompletedTask;
    }
}

internal sealed class FakeUpdateCheckRecord : IUpdateCheckRecord
{
    public LastUpdateCheck? Saved { get; set; }

    public Task<LastUpdateCheck?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

    public Task WriteAsync(LastUpdateCheck check, CancellationToken cancellationToken)
    {
        Saved = check;

        return Task.CompletedTask;
    }
}
