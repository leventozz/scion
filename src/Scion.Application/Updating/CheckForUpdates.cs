using Scion.Domain;

namespace Scion.Application.Updating;

/// <summary>
/// Looks for a newer Scion. The start of Scion looks only when the user wants that and at most once a day; within the day it
/// shows the update an earlier look found. A check the user asks for always looks.
/// </summary>
public sealed class CheckForUpdates(
    IUpdateFeed feed, IUpdateSettings settings, IUpdateCheckRecord record, IScionInstallation installation, TimeProvider time)
{
    public async Task<Result<UpdateCheck, UpdateError>> ExecuteAsync(
        UpdateCheckTrigger trigger, CancellationToken cancellationToken = default)
    {
        if (!installation.IsInstalled)
        {
            return Succeeded(new UpdateCheck.RunningFromSource());
        }

        if (trigger == UpdateCheckTrigger.Startup && await StartupNeedsNoQuestionAsync(cancellationToken) is { } answer)
        {
            return Succeeded(answer);
        }

        try
        {
            var found = await feed.CheckAsync(cancellationToken);
            await NoteAsync(found, cancellationToken);

            return Succeeded(found);
        }
        catch (UpdateFailedException exception)
        {
            return Result<UpdateCheck, UpdateError>.Failure(new UpdateError(exception.Message));
        }
    }

    /// <returns>What the start of Scion shows without asking the feed; null when the feed is to be asked.</returns>
    private async Task<UpdateCheck?> StartupNeedsNoQuestionAsync(CancellationToken cancellationToken)
    {
        if (!(await settings.ReadAsync(cancellationToken)).CheckWhenScionStarts)
        {
            return new UpdateCheck.Skipped();
        }

        var last = await record.ReadAsync(cancellationToken);
        if (last is null || time.GetUtcNow() - last.CheckedAt >= UpdateCheckPolicy.Interval)
        {
            return null;
        }

        return last.Found is { } update && last.CheckedOnVersion == installation.Version
            ? new UpdateCheck.Available(update)
            : new UpdateCheck.Skipped();
    }

    private async Task NoteAsync(UpdateCheck found, CancellationToken cancellationToken)
    {
        if (found is not (UpdateCheck.Available or UpdateCheck.UpToDate))
        {
            return;
        }

        try
        {
            var update = (found as UpdateCheck.Available)?.Update;
            await record.WriteAsync(new LastUpdateCheck(time.GetUtcNow(), installation.Version, update), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static Result<UpdateCheck, UpdateError> Succeeded(UpdateCheck check) => Result<UpdateCheck, UpdateError>.Success(check);
}
