namespace Scion.Application.Adapting;

/// <summary>
/// Starts the provider's own sign-in and waits: it ends when the provider is <see cref="AiProviderState.Ready"/>, when
/// the sign-in process ends, or when the user cancels. The sign-in process is ended in every case.
/// </summary>
public sealed class SignInToAiProvider(IAiProviderPorts<IAiProviderStatus> statuses, TimeProvider timeProvider)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <returns>The state the provider is in when the wait ends; anything but Ready if the user cancelled.</returns>
    public async Task<AiProviderState> ExecuteAsync(AiProviderId provider, CancellationToken cancellationToken = default)
    {
        var status = statuses.For(provider);
        using var signInLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var signIn = status.StartSignInAsync(signInLifetime.Token);

        try
        {
            return await WaitForOutcomeAsync(status, signIn, signInLifetime.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AiProviderState.SignedOut;
        }
        finally
        {
            await EndAsync(signIn, signInLifetime);
        }
    }

    private async Task<AiProviderState> WaitForOutcomeAsync(
        IAiProviderStatus status, Task signIn, CancellationToken cancellationToken)
    {
        while (true)
        {
            var poll = Task.Delay(PollInterval, timeProvider, cancellationToken);

            // The sign-in process ending is not waited for until the next poll: the state it left is what counts.
            var ended = await Task.WhenAny(signIn, poll) == signIn;
            cancellationToken.ThrowIfCancellationRequested();
            await (ended ? Task.CompletedTask : poll);

            var state = await status.GetStateAsync(cancellationToken);
            if (state == AiProviderState.Ready || ended)
            {
                return state;
            }
        }
    }

    private static async Task EndAsync(Task signIn, CancellationTokenSource signInLifetime)
    {
        await signInLifetime.CancelAsync();

        try
        {
            await signIn;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
