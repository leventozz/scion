using Scion.Application.Updating;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace Scion.Infrastructure.Velopack;

/// <summary>
/// Velopack behind the three update ports. The source is the address of a GitHub repository (<c>http…</c>) or a folder with
/// releases. What a check found is kept, because a download and the apply that follows it are about that same release.
/// </summary>
/// <param name="source">A GitHub repository address, or a folder that holds <c>releases.win.json</c> and the packages.</param>
/// <param name="locator">Where Scion is installed. Null for the real one, which <c>VelopackApp.Build().Run()</c> at the start of Scion has found.</param>
public sealed class VelopackUpdates(string source, IVelopackLocator? locator = null)
    : IUpdateFeed, IUpdateDownloader, IUpdateApplier
{
    private readonly Lazy<UpdateManager> _manager = new(() => new UpdateManager(UpdateSourceFor(source), null, locator));

    private UpdateInfo? _found;

    public async Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manager = _manager.Value;
            if (!manager.IsInstalled || manager.CurrentVersion is not { } current)
            {
                return new UpdateCheck.RunningFromSource();
            }

            _found = await manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);

            return _found is null
                ? new UpdateCheck.UpToDate(current.ToString())
                : new UpdateCheck.Available(new AvailableUpdate(_found.TargetFullRelease.Version.ToString(), _found.TargetFullRelease.NotesMarkdown));
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

        var found = Found(update) ?? throw new UpdateFailedException($"Scion has not looked up version {update.Version} yet.");

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
        var found = Found(update) ?? throw new InvalidOperationException($"Version {update.Version} was never looked up, so it was not downloaded.");

        _manager.Value.WaitExitThenApplyUpdates(found.TargetFullRelease, silent: false, restart: true);
    }

    private UpdateInfo? Found(AvailableUpdate update) =>
        _found is { } found && found.TargetFullRelease.Version.ToString() == update.Version ? found : null;

    private static IUpdateSource UpdateSourceFor(string source) =>
        source.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new GithubSource(source, null, prerelease: false)
            : new SimpleFileSource(new DirectoryInfo(source));
}
