using Scion.Application.Updating;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace Scion.Infrastructure.Velopack;

/// <summary>
/// Velopack behind the update ports. The source is the address of a GitHub repository (<c>http…</c>) or a folder with
/// releases. What a check found is kept, because a download and the apply that follows it are about that same release; an update
/// remembered from an earlier start is looked up again before it is downloaded.
/// </summary>
/// <param name="source">A GitHub repository address, or a folder that holds <c>releases.win.json</c> and the packages.</param>
/// <param name="fallbackVersion">The version of this build, told as the version of a Scion that is not installed.</param>
/// <param name="locator">Where Scion is installed. Null for the real one, which <c>VelopackApp.Build().Run()</c> at the start of Scion has found.</param>
public sealed class VelopackUpdates(string source, string fallbackVersion, IVelopackLocator? locator = null)
    : IUpdateFeed, IUpdateDownloader, IUpdateApplier, IScionInstallation
{
    private readonly Lazy<UpdateManager> _manager = new(() => new UpdateManager(UpdateSourceFor(source), null, locator));

    private UpdateInfo? _found;

    public bool IsInstalled => Installed() is not null;

    public string Version => Installed()?.ToString() ?? fallbackVersion;

    public async Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Installed() is not { } current)
            {
                return new UpdateCheck.RunningFromSource();
            }

            _found = await _manager.Value.CheckForUpdatesAsync().WaitAsync(cancellationToken);

            return _found is null
                ? new UpdateCheck.UpToDate(current.ToString())
                : new UpdateCheck.Available(Offered(_found));
        }
        catch (NotInstalledException)
        {
            return new UpdateCheck.RunningFromSource();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UpdateFailedException(exception.Message);
        }
    }

    public async Task DownloadAsync(AvailableUpdate update, IProgress<int> percent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var found = await FoundAsync(update, cancellationToken);

        try
        {
            await _manager.Value.DownloadUpdatesAsync(found, percent.Report, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UpdateFailedException(exception.Message);
        }
    }

    public void ApplyWhenScionExits(AvailableUpdate update)
    {
        var found = Found(update) ?? throw new UpdateFailedException($"Version {update.Version} was never looked up, so it was not downloaded.");

        try
        {
            _manager.Value.WaitExitThenApplyUpdates(found.TargetFullRelease, silent: false, restart: true);
        }
        catch (Exception exception)
        {
            throw new UpdateFailedException(exception.Message);
        }
    }

    /// <returns>The installed version; null when Scion is not installed, or when nothing says which version it is.</returns>
    private SemanticVersion? Installed()
    {
        try
        {
            return _manager.Value.IsInstalled ? _manager.Value.CurrentVersion : null;
        }
        catch (NotInstalledException)
        {
            return null;
        }
    }

    private async Task<UpdateInfo> FoundAsync(AvailableUpdate update, CancellationToken cancellationToken)
    {
        if (Found(update) is { } known)
        {
            return known;
        }

        try
        {
            _found = await _manager.Value.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UpdateFailedException(exception.Message);
        }

        return Found(update) ?? throw new UpdateFailedException($"Version {update.Version} is no longer offered.");
    }

    private UpdateInfo? Found(AvailableUpdate update) =>
        _found is { } found && found.TargetFullRelease.Version.ToString() == update.Version ? found : null;

    private static AvailableUpdate Offered(UpdateInfo found) =>
        new(found.TargetFullRelease.Version.ToString(), found.TargetFullRelease.NotesMarkdown is { } notes && !string.IsNullOrWhiteSpace(notes) ? notes : null);

    private static IUpdateSource UpdateSourceFor(string source) =>
        source.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new GithubSource(source, null, prerelease: false)
            : new SimpleFileSource(new DirectoryInfo(source));
}
