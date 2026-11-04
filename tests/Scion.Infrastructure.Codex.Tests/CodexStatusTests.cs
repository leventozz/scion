using Scion.Application.Adapting;

namespace Scion.Infrastructure.Codex.Tests;

[Collection("FakeCodex")]
public class CodexStatusTests : IDisposable
{
    private readonly FakeCodex _fake = new();
    private readonly IDisposable _environment;

    public CodexStatusTests() => _environment = _fake.UseAsEnvironment();

    public void Dispose()
    {
        _environment.Dispose();
        _fake.Dispose();
    }

    private CodexStatus Status(string? executablePath = null) =>
        new(new CodexExecutable(executablePath ?? _fake.ExecutablePath, "/unused", _ => null));

    private static string NoSuchCodex => Path.Combine(Path.GetTempPath(), "scion-tests", "no-such-codex");

    [Fact]
    public async Task Is_ready_when_login_status_exits_with_zero()
    {
        _fake.Write(new { signedIn = true });

        Assert.Equal(AiProviderState.Ready, await Status().GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Is_signed_out_when_login_status_exits_with_anything_else()
    {
        _fake.Write(new { signedIn = false });

        Assert.Equal(AiProviderState.SignedOut, await Status().GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Is_not_installed_when_there_is_no_executable() =>
        Assert.Equal(AiProviderState.NotInstalled, await Status(NoSuchCodex).GetStateAsync(CancellationToken.None));

    [Fact]
    public async Task Asks_with_login_status_in_an_empty_folder_and_nothing_else()
    {
        await Status().GetStateAsync(CancellationToken.None);

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(["login", "status"], call.Args);
        Assert.Equal(0, call.CwdEntries);
        Assert.False(call.ApiKeySet);
    }

    [Fact]
    public async Task Claims_nothing_about_how_it_is_paid_for_because_exec_prefers_an_api_key_to_the_sign_in()
    {
        _fake.Write(new { signedIn = true });

        Assert.Equal(AiUsageBasis.Unknown, await Status().ReadAsync(CancellationToken.None));
        Assert.Empty(_fake.Calls());
    }

    [Fact]
    public async Task Starts_the_sign_in_of_codex_itself_and_ends_when_that_process_ends()
    {
        _fake.Write(new { signedIn = false, loginDelayMs = 50 });
        var status = Status();

        await status.StartSignInAsync(CancellationToken.None);

        Assert.Equal(["login"], Assert.Single(_fake.Calls()).Args);
        Assert.Equal(AiProviderState.Ready, await status.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Signing_in_without_codex_installed_ends_at_once()
    {
        await Status(NoSuchCodex).StartSignInAsync(CancellationToken.None);

        Assert.Empty(_fake.Calls());
    }

    [Fact]
    public async Task Cancelling_the_sign_in_ends_the_process()
    {
        _fake.Write(new { signedIn = false, loginDelayMs = -1 });
        using var cancellation = new CancellationTokenSource();

        var task = Status().StartSignInAsync(cancellation.Token);
        await _fake.WaitUntilStartedAsync();
        await cancellation.CancelAsync();
        await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(task.IsCompleted);
        await WaitUntilAsync(() => !_fake.IsRunning());
        Assert.False(_fake.IsRunning());
        Assert.Equal(AiProviderState.SignedOut, await Status().GetStateAsync(CancellationToken.None));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
