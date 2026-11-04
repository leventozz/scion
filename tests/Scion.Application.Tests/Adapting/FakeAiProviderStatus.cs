using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>A provider whose state a test sets, and whose sign-in runs until the test ends it or the token is cancelled.</summary>
internal sealed class FakeAiProviderStatus : IAiProviderStatus
{
    private readonly TaskCompletionSource _signIn = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AiProviderState State { get; set; } = AiProviderState.SignedOut;

    public int StateChecks { get; private set; }

    public bool SignInStarted { get; private set; }

    public bool SignInCancelled { get; private set; }

    public void EndSignIn() => _signIn.TrySetResult();

    public Task<AiProviderState> GetStateAsync(CancellationToken cancellationToken)
    {
        StateChecks++;

        return Task.FromResult(State);
    }

    public async Task StartSignInAsync(CancellationToken cancellationToken)
    {
        SignInStarted = true;

        await using var registration = cancellationToken.Register(() =>
        {
            SignInCancelled = true;
            _signIn.TrySetCanceled(cancellationToken);
        });

        await _signIn.Task;
    }
}
