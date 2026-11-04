namespace Scion.Application.Updating;

/// <summary>Where the releases of Scion are listed.</summary>
public interface IUpdateFeed
{
    /// <exception cref="UpdateFailedException">The feed could not be read.</exception>
    Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken);
}
