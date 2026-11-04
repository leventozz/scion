namespace Scion.Application.Adapting;

/// <summary>Scion's own note of the provider the user adapted with last. It never touches a user's repository.</summary>
public interface ILastUsedAiProvider
{
    /// <returns>The provider used last, or null when there is no note (or it cannot be read).</returns>
    Task<AiProviderId?> ReadAsync(CancellationToken cancellationToken);

    /// <exception cref="IOException">The note could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The note could not be written.</exception>
    Task RememberAsync(AiProviderId provider, CancellationToken cancellationToken);
}
