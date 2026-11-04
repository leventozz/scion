using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode.Tests;

[Collection("FakeClaude")]
public class ClaudeCodeStatusTests : IDisposable
{
    private readonly FakeClaude _fake = new();
    private readonly IDisposable _environment;

    public ClaudeCodeStatusTests() => _environment = _fake.UseAsEnvironment();

    public void Dispose()
    {
        _environment.Dispose();
        _fake.Dispose();
    }

    private ClaudeCodeStatus Status(string? executablePath = null) =>
        new(new ClaudeCodeExecutable(executablePath ?? _fake.ExecutablePath, "/unused", _ => null));

    [Fact]
    public async Task Is_ready_when_auth_status_exits_with_zero()
    {
        _fake.Write(new { signedIn = true });

        Assert.Equal(AiProviderState.Ready, await Status().GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Is_signed_out_when_auth_status_exits_with_one()
    {
        _fake.Write(new { signedIn = false });

        Assert.Equal(AiProviderState.SignedOut, await Status().GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Is_not_installed_when_there_is_no_executable()
    {
        var state = await Status(Path.Combine(Path.GetTempPath(), "scion-tests", "no-such-claude")).GetStateAsync(CancellationToken.None);

        Assert.Equal(AiProviderState.NotInstalled, state);
    }

    [Fact]
    public async Task Asks_with_auth_status_in_an_empty_folder_and_nothing_else()
    {
        await Status().GetStateAsync(CancellationToken.None);

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(["auth", "status"], call.Args);
        Assert.Equal(0, call.CwdEntries);
        Assert.False(call.ApiKeySet);
    }

    [Theory]
    [InlineData("claude.ai", AiUsageBasis.Subscription)]
    [InlineData("oauth_token", AiUsageBasis.Subscription)]
    [InlineData("api_key", AiUsageBasis.ApiKey)]
    [InlineData("api_key_helper", AiUsageBasis.ApiKey)]
    [InlineData("third_party", AiUsageBasis.Unknown)]
    [InlineData("none", AiUsageBasis.Unknown)]
    [InlineData("something-new", AiUsageBasis.Unknown)]
    [InlineData(null, AiUsageBasis.Unknown)]
    public async Task Takes_the_usage_basis_from_the_auth_method_claude_code_reports(string? authMethod, AiUsageBasis expected)
    {
        _fake.Write(new { signedIn = true, authMethod, extraAuthFields = true });

        Assert.Equal(expected, await Status().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Asks_only_with_auth_status_to_read_the_usage_basis()
    {
        _fake.Write(new { signedIn = true, authMethod = "claude.ai" });

        await Status().ReadAsync(CancellationToken.None);

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(["auth", "status"], call.Args);
        Assert.False(call.ApiKeySet);
    }

    [Fact]
    public async Task Claims_nothing_when_signed_out_not_installed_or_the_answer_is_not_json()
    {
        _fake.Write(new { signedIn = false, authMethod = "claude.ai" });
        Assert.Equal(AiUsageBasis.Unknown, await Status().ReadAsync(CancellationToken.None));

        Assert.Equal(
            AiUsageBasis.Unknown,
            await Status(Path.Combine(Path.GetTempPath(), "scion-tests", "no-such-claude")).ReadAsync(CancellationToken.None));

        _fake.Write(new { signedIn = true, authStatusNotJson = true });
        Assert.Equal(AiUsageBasis.Unknown, await Status().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Starts_the_sign_in_of_claude_code_itself_and_ends_when_that_process_ends()
    {
        _fake.Write(new { signedIn = false, loginDelayMs = 50 });
        var status = Status();

        await status.StartSignInAsync(CancellationToken.None);

        Assert.Equal(["auth", "login"], Assert.Single(_fake.Calls()).Args);
        Assert.Equal(AiProviderState.Ready, await status.GetStateAsync(CancellationToken.None));
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
