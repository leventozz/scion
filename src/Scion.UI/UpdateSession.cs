using Scion.Application.Updating;
using Scion.Domain;

namespace Scion.UI;

/// <summary>Where the update of Scion is: nothing on offer, on offer, being downloaded, downloaded, or failed.</summary>
public enum UpdateStage
{
    Idle,
    Available,
    Downloading,
    Ready,
    Failed,
}

/// <summary>Which step of an update failed; it decides what "Try again" does.</summary>
public enum UpdateFailure
{
    Download,
    Install,
}

/// <summary>
/// The update of Scion as the user sees it. It outlives the screens (one per Scion), so going from one screen to another loses
/// nothing; the screens only show it and tell it what the user chose. A plain class: the use cases do the work.
/// It raises <see cref="Changed"/> from whatever thread changed it (a download reports from its own); whoever shows it moves to
/// the renderer.
/// </summary>
public sealed class UpdateSession(
    CheckForUpdates check, DownloadUpdate download, ApplyUpdate apply, IScionInstallation installation, CloseGuard closeGuard)
{
    private CancellationTokenSource? _downloading;
    private int _percent;

    public UpdateStage Stage { get; private set; }

    /// <summary>The update on offer, downloaded or failed; null while <see cref="Stage"/> is <see cref="UpdateStage.Idle"/>.</summary>
    public AvailableUpdate? Update { get; private set; }

    /// <summary>How much of the download is done, 0 to 100.</summary>
    public int Percent => Volatile.Read(ref _percent);

    public UpdateFailure? Failure { get; private set; }

    /// <summary>Whether the dialog about the update is open. It is the user's to open (or opened by a check they asked for).</summary>
    public bool IsDialogOpen { get; private set; }

    /// <summary>The version of this Scion, for the lines that say what the user has.</summary>
    public string CurrentVersion => installation.Version;

    public event Action? Changed;

    /// <summary>
    /// The check Scion makes when it starts. It runs in the background, so it never fails loudly: what it cannot find out, it
    /// does not show.
    /// </summary>
    public async Task StartupCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await check.ExecuteAsync(UpdateCheckTrigger.Startup, cancellationToken);

            if (Stage == UpdateStage.Idle && result is { IsSuccess: true, Value: UpdateCheck.Available found })
            {
                PutOnOffer(found.Update, openDialog: false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A check nobody asked for must not take Scion down; the next start looks again.
        }
    }

    /// <summary>The check the user asked for. What it found is theirs to be told: an update opens the dialog about it.</summary>
    public async Task<Result<UpdateCheck, UpdateError>> ManualCheckAsync(CancellationToken cancellationToken = default)
    {
        var result = await check.ExecuteAsync(UpdateCheckTrigger.Manual, cancellationToken);

        if (result is { IsSuccess: true, Value: UpdateCheck.Available found })
        {
            if (IsBeingInstalled(found.Update))
            {
                OpenDialog();
            }
            else
            {
                PutOnOffer(found.Update, openDialog: true);
            }
        }

        return result;
    }

    public void OpenDialog()
    {
        if (Update is null)
        {
            return;
        }

        IsDialogOpen = true;
        Changed?.Invoke();
    }

    /// <summary>The user decided to do it later: the dialog closes and the update stays on offer.</summary>
    public void Later()
    {
        IsDialogOpen = false;
        Changed?.Invoke();
    }

    public async Task DownloadAsync()
    {
        if (Update is not { } update || Stage == UpdateStage.Downloading)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _downloading = cancellation;
        Failure = null;
        Volatile.Write(ref _percent, 0);
        Stage = UpdateStage.Downloading;
        Changed?.Invoke();

        var result = await download.ExecuteAsync(update, new ActionProgress<int>(OnPercent), cancellation.Token);
        _downloading = null;

        switch (result)
        {
            case UpdateDownload.Downloaded:
                Stage = UpdateStage.Ready;
                break;
            case UpdateDownload.Cancelled:
                Stage = UpdateStage.Available;
                IsDialogOpen = false;
                break;
            case UpdateDownload.Failed:
                Stage = UpdateStage.Failed;
                Failure = UpdateFailure.Download;
                break;
        }

        Changed?.Invoke();
    }

    public void CancelDownload() => _downloading?.Cancel();

    /// <summary>
    /// Installs the downloaded update and starts Scion again. A draft in the way is asked about first, and nothing is arranged if the user
    /// keeps it: the order is the question, then the installation, then closing the window.
    /// </summary>
    public async Task RestartAsync()
    {
        if (Update is not { } update || !await closeGuard.ConfirmLeaveAsync())
        {
            return;
        }

        if (apply.Execute(update) is UpdateApply.Failed)
        {
            Stage = UpdateStage.Failed;
            Failure = UpdateFailure.Install;
            IsDialogOpen = true;
            Changed?.Invoke();

            return;
        }

        closeGuard.CloseNow();
    }

    public Task TryAgainAsync() => Failure == UpdateFailure.Download ? DownloadAsync() : RestartAsync();

    private bool IsBeingInstalled(AvailableUpdate found) =>
        Update?.Version == found.Version && Stage is UpdateStage.Downloading or UpdateStage.Ready;

    private void PutOnOffer(AvailableUpdate update, bool openDialog)
    {
        Update = update;
        Stage = UpdateStage.Available;
        Failure = null;
        IsDialogOpen = openDialog;
        Changed?.Invoke();
    }

    private void OnPercent(int percent)
    {
        Volatile.Write(ref _percent, percent);
        Changed?.Invoke();
    }
}
