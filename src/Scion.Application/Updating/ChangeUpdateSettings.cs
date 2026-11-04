namespace Scion.Application.Updating;

/// <summary>Saves the user's choice about updating. A choice that cannot be written never stops the user.</summary>
public sealed class ChangeUpdateSettings(IUpdateSettings settings)
{
    public async Task ExecuteAsync(UpdateSettings changed, CancellationToken cancellationToken = default)
    {
        try
        {
            await settings.WriteAsync(changed, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
