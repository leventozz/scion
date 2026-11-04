using Scion.Domain;

namespace Scion.Application.Updating;

/// <summary>
/// Looks for a newer Scion. The start of Scion looks only when the user wants that; a check the user asks for always looks.
/// </summary>
public sealed class CheckForUpdates(IUpdateFeed feed, IUpdateSettings settings)
{
    public async Task<Result<UpdateCheck, UpdateError>> ExecuteAsync(
        UpdateCheckTrigger trigger, CancellationToken cancellationToken = default)
    {
        if (trigger == UpdateCheckTrigger.Startup && !(await settings.ReadAsync(cancellationToken)).CheckWhenScionStarts)
        {
            return Result<UpdateCheck, UpdateError>.Success(new UpdateCheck.Skipped());
        }

        try
        {
            return Result<UpdateCheck, UpdateError>.Success(await feed.CheckAsync(cancellationToken));
        }
        catch (UpdateFailedException exception)
        {
            return Result<UpdateCheck, UpdateError>.Failure(new UpdateError(exception.Message));
        }
    }
}
