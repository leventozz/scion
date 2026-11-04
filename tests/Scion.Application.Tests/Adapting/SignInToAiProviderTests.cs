using Microsoft.Extensions.Time.Testing;
using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class SignInToAiProviderTests
{
    private static readonly AiProviderId Provider = new("sample");

    private readonly FakeAiProviderStatus _status = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly SignInToAiProvider _signIn;

    public SignInToAiProviderTests() => _signIn = new SignInToAiProvider(new AiProviderPortsStub<IAiProviderStatus>().With("sample", _status), _clock);

    [Fact]
    public async Task Starts_the_sign_in_of_the_provider()
    {
        using var cancellation = new CancellationTokenSource();

        var task = _signIn.ExecuteAsync(Provider, cancellation.Token);

        Assert.True(_status.SignInStarted);
        await cancellation.CancelAsync();
        await task;
    }

    [Fact]
    public async Task Signs_in_only_to_the_provider_it_was_given()
    {
        var other = new FakeAiProviderStatus();
        var signIn = new SignInToAiProvider(
            new AiProviderPortsStub<IAiProviderStatus>().With("sample", _status).With("other", other), _clock);
        using var cancellation = new CancellationTokenSource();

        var task = signIn.ExecuteAsync(new AiProviderId("other"), cancellation.Token);

        Assert.True(other.SignInStarted);
        Assert.False(_status.SignInStarted);
        await cancellation.CancelAsync();
        await task;
    }

    [Fact]
    public async Task Ends_with_ready_as_soon_as_the_provider_is_ready()
    {
        var task = _signIn.ExecuteAsync(Provider);

        _status.State = AiProviderState.Ready;
        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(AiProviderState.Ready, await task);
    }

    [Fact]
    public async Task Stops_the_sign_in_process_once_ready()
    {
        var task = _signIn.ExecuteAsync(Provider);

        _status.State = AiProviderState.Ready;
        _clock.Advance(TimeSpan.FromSeconds(2));
        await task;

        Assert.True(_status.SignInCancelled);
    }

    [Fact]
    public async Task Checks_the_state_after_two_seconds()
    {
        using var cancellation = new CancellationTokenSource();
        var task = _signIn.ExecuteAsync(Provider, cancellation.Token);

        _clock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal(0, _status.StateChecks);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitForChecksAsync(1);

        await cancellation.CancelAsync();
        await task;
    }

    [Fact]
    public async Task Ends_without_ready_when_the_user_cancels_and_stops_the_sign_in_process()
    {
        using var cancellation = new CancellationTokenSource();
        var task = _signIn.ExecuteAsync(Provider, cancellation.Token);

        await cancellation.CancelAsync();
        var state = await task;

        Assert.Equal(AiProviderState.SignedOut, state);
        Assert.True(_status.SignInCancelled);
    }

    [Fact]
    public async Task Ends_with_the_current_state_when_the_sign_in_process_ends_on_its_own()
    {
        var task = _signIn.ExecuteAsync(Provider);

        _status.EndSignIn();
        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(AiProviderState.SignedOut, await task);
    }

    [Fact]
    public async Task Is_ready_when_the_sign_in_process_ends_after_the_user_finished()
    {
        var task = _signIn.ExecuteAsync(Provider);

        _status.State = AiProviderState.Ready;
        _status.EndSignIn();
        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(AiProviderState.Ready, await task);
    }

    private async Task WaitForChecksAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_status.StateChecks < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Equal(count, _status.StateChecks);
    }
}
