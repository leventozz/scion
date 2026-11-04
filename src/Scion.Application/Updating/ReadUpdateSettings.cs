namespace Scion.Application.Updating;

/// <summary>Gives the user's choice about updating, for the screen that lets them change it.</summary>
public sealed class ReadUpdateSettings(IUpdateSettings settings)
{
    public Task<UpdateSettings> ExecuteAsync(CancellationToken cancellationToken = default) => settings.ReadAsync(cancellationToken);
}
