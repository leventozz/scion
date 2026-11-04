using Scion.Application.Adapting;

namespace Scion.UI.Tests;

/// <summary>A provider whose state a test sets; its sign-in runs until the test ends it or the token is cancelled.</summary>
internal sealed class FakeAiProviderStatus : IAiProviderStatus, IAiUsageBasisReader
{
    private TaskCompletionSource _signIn = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AiProviderState State { get; set; } = AiProviderState.Ready;

    public AiUsageBasis UsageBasis { get; set; } = AiUsageBasis.Subscription;

    public int SignInsStarted { get; private set; }

    public bool SignInCancelled { get; private set; }

    /// <summary>The user finishes in the browser: the provider is ready and the sign-in process ends.</summary>
    public void FinishSignIn()
    {
        State = AiProviderState.Ready;
        _signIn.TrySetResult();
    }

    public Task<AiUsageBasis> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(UsageBasis);

    public Task<AiProviderState> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(State);

    public async Task StartSignInAsync(CancellationToken cancellationToken)
    {
        SignInsStarted++;
        _signIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var registration = cancellationToken.Register(() =>
        {
            SignInCancelled = true;
            _signIn.TrySetCanceled(cancellationToken);
        });

        await _signIn.Task;
    }
}
