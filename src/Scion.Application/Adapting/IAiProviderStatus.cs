namespace Scion.Application.Adapting;

public interface IAiProviderStatus
{
    Task<AiProviderState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the provider's own sign-in and returns when that sign-in process has ended. When the token is cancelled
    /// the process is ended and the task completes, or throws <see cref="OperationCanceledException"/>. Scion never sees
    /// the credentials: the provider's client does the login.
    /// </summary>
    Task StartSignInAsync(CancellationToken cancellationToken);
}
