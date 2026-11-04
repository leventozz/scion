namespace Scion.Application.Updating;

/// <summary>Scion's own note of the last check for updates. It never touches a user's repository.</summary>
public interface IUpdateCheckRecord
{
    /// <returns>The note; null when nothing was noted or what was noted cannot be read.</returns>
    Task<LastUpdateCheck?> ReadAsync(CancellationToken cancellationToken);

    /// <exception cref="IOException">The note could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The note could not be written.</exception>
    Task WriteAsync(LastUpdateCheck check, CancellationToken cancellationToken);
}
