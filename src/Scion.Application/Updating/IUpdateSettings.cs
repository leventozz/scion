namespace Scion.Application.Updating;

/// <summary>Scion's own settings about updating. They never touch a user's repository.</summary>
public interface IUpdateSettings
{
    /// <returns>The settings; the defaults when nothing was saved or what was saved cannot be read.</returns>
    Task<UpdateSettings> ReadAsync(CancellationToken cancellationToken);

    /// <exception cref="IOException">The settings could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The settings could not be written.</exception>
    Task WriteAsync(UpdateSettings settings, CancellationToken cancellationToken);
}
