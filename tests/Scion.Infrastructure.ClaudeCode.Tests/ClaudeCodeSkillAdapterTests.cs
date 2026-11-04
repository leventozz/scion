using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode.Tests;

[Collection("FakeClaude")]
public class ClaudeCodeSkillAdapterTests : IDisposable
{
    private static readonly AdaptationPrompt Prompt = new("Return the skill.", "<<< SKILL.md from docker >>>\n# Docker — ünïcode\n");

    private readonly FakeClaude _fake = new(new { adapt = new { kind = "success", skillFile = "---\nname: docker\n---\n" } });
    private readonly IDisposable _environment;
    private readonly RecordingProgress _progress = new();

    public ClaudeCodeSkillAdapterTests() => _environment = _fake.UseAsEnvironment();

    public void Dispose()
    {
        _environment.Dispose();
        _fake.Dispose();
    }

    private ClaudeCodeSkillAdapter AdapterFor(string? executablePath = null)
    {
        var executable = new ClaudeCodeExecutable(executablePath ?? _fake.ExecutablePath, "/unused", _ => null);

        return new ClaudeCodeSkillAdapter(executable, new ClaudeCodeStatus(executable));
    }

    private Task<Scion.Domain.Result<string, AdaptationFailure>> AdaptAsync(CancellationToken cancellationToken = default) =>
        AdapterFor().AdaptAsync(Prompt, _progress, cancellationToken);

    [Fact]
    public async Task Passes_exactly_the_documented_flags_and_the_instructions_as_the_system_prompt()
    {
        _fake.Write(new { adapt = new { kind = "success", skillFile = "x" } });

        await AdaptAsync();

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(
            [
                "-p",
                "--output-format", "stream-json",
                "--verbose",
                "--include-partial-messages",
                "--json-schema", "{\"type\":\"object\",\"properties\":{\"skillFile\":{\"type\":\"string\"}},\"required\":[\"skillFile\"],\"additionalProperties\":false}",
                "--tools", "",
                "--disallowedTools", "mcp__*",
                "--strict-mcp-config",
                "--safe-mode",
                "--no-session-persistence",
                "--system-prompt", Prompt.Instructions,
            ],
            call.Args);
    }

    [Fact]
    public async Task Never_uses_bare_mode_because_that_would_not_read_the_users_sign_in()
    {
        await AdaptAsync();

        Assert.DoesNotContain("--bare", Assert.Single(_fake.Calls()).Args);
    }

    [Fact]
    public async Task Sends_the_content_of_the_prompt_on_standard_input_and_nothing_else()
    {
        await AdaptAsync();

        Assert.Equal(Prompt.Content, Assert.Single(_fake.Calls()).Stdin);
    }

    [Fact]
    public async Task Runs_in_a_new_empty_folder_that_is_removed_afterwards()
    {
        await AdaptAsync();

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(0, call.CwdEntries);
        Assert.NotEqual(Directory.GetCurrentDirectory(), call.Cwd);
        Assert.False(Directory.Exists(call.Cwd));
    }

    [Fact]
    public async Task Runs_every_call_in_its_own_folder()
    {
        await AdaptAsync();
        await AdaptAsync();

        Assert.Equal(2, _fake.Calls().Select(call => call.Cwd).Distinct().Count());
    }

    [Fact]
    public async Task Leaves_the_environment_as_it_is_and_never_sets_an_api_key()
    {
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("SCION_FAKE_CLAUDE_MARKER", "inherited");
        try
        {
            await AdaptAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
            Environment.SetEnvironmentVariable("SCION_FAKE_CLAUDE_MARKER", null);
        }

        var call = Assert.Single(_fake.Calls());
        Assert.False(call.ApiKeySet);
        Assert.Equal("inherited", call.Marker);
    }

    [Fact]
    public async Task Gives_the_skill_file_of_the_structured_output()
    {
        _fake.Write(new { adapt = new { kind = "success", skillFile = "---\nname: docker\n---\nBody — ünïcode\n" } });

        var result = await AdaptAsync();

        Assert.Equal("---\nname: docker\n---\nBody — ünïcode\n", result.Value);
    }

    [Fact]
    public async Task Falls_back_to_the_result_text_when_there_is_no_structured_output()
    {
        _fake.Write(new { adapt = new { kind = "resultOnly", skillFile = "---\nname: docker\n---\n" } });

        var result = await AdaptAsync();

        Assert.Equal("---\nname: docker\n---\n", result.Value);
    }

    [Fact]
    public async Task A_rate_limit_status_is_a_usage_limit()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "limitByStatus" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.UsageLimitReached>(result.Error);
    }

    [Fact]
    public async Task A_message_that_says_the_limit_was_hit_is_a_usage_limit()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "limitByText" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.UsageLimitReached>(result.Error);
    }

    [Fact]
    public async Task An_error_while_signed_out_is_signed_out()
    {
        _fake.Write(new { signedIn = false, adapt = new { kind = "error", message = "Not logged in" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.SignedOut>(result.Error);
        Assert.Equal(["auth", "status"], _fake.Calls().Last().Args);
    }

    [Fact]
    public async Task An_error_while_signed_in_is_a_failure_with_the_message()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "error", message = "The model is overloaded" } });

        var result = await AdaptAsync();

        Assert.Equal(new AdaptationFailure.Failed("The model is overloaded"), result.Error);
    }

    [Fact]
    public async Task Output_that_is_not_json_is_a_failure()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "notJson" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.Failed>(result.Error);
    }

    [Fact]
    public async Task An_empty_skill_file_is_a_failure()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "success", skillFile = "" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.Failed>(result.Error);
    }

    [Fact]
    public async Task A_missing_executable_is_not_installed()
    {
        var result = await AdapterFor(Path.Combine(Path.GetTempPath(), "scion-tests", "no-such-claude")).AdaptAsync(Prompt, _progress, CancellationToken.None);

        Assert.IsType<AdaptationFailure.NotInstalled>(result.Error);
    }

    [Fact]
    public async Task Cancelling_ends_the_process_and_is_cancelled()
    {
        _fake.Write(new { adapt = new { kind = "hang" } });
        using var cancellation = new CancellationTokenSource();

        var task = AdaptAsync(cancellation.Token);
        await _fake.WaitUntilStartedAsync();
        await cancellation.CancelAsync();
        var result = await task;

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
        await WaitUntilAsync(() => !_fake.IsRunning());
        Assert.False(_fake.IsRunning());
    }

    [Fact]
    public async Task A_token_that_is_already_cancelled_starts_nothing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await AdaptAsync(cancellation.Token);

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
        Assert.Empty(_fake.Calls());
    }

    [Fact]
    public async Task Reports_the_text_in_pieces_while_it_arrives_and_returns_the_complete_file()
    {
        const string SkillFile = "---\nname: docker\ndescription: \"Quoted\" — ünïcode \U0001F600\n---\n# Docker\n";
        _fake.Write(new { adapt = new { kind = "success", skillFile = SkillFile, chunkSize = 5 } });

        var result = await AdaptAsync();

        Assert.True(_progress.Texts.Count() > 3);
        Assert.Equal(SkillFile, string.Concat(_progress.Texts));
        Assert.Equal(SkillFile, result.Value);
    }

    [Fact]
    public async Task Reports_the_pieces_before_the_program_has_ended()
    {
        _fake.Write(new { adapt = new { kind = "success", skillFile = "---\nname: docker\n---\n", chunkSize = 4, hangAfterChunks = 5 } });
        using var cancellation = new CancellationTokenSource();

        var task = AdaptAsync(cancellation.Token);
        await WaitUntilAsync(() => string.Concat(_progress.Texts) == "---\nn");

        Assert.False(task.IsCompleted);
        Assert.Equal("---\nn", string.Concat(_progress.Texts));
        await cancellation.CancelAsync();
        await task;
    }

    [Fact]
    public async Task A_stream_that_stops_before_the_result_is_a_failure_and_the_streamed_text_is_not_the_answer()
    {
        _fake.Write(new { signedIn = true, adapt = new { kind = "success", skillFile = "---\nname: docker\n---\nBody\n", chunkSize = 6, stopAfterChunks = 3 } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.Failed>(result.Error);
        Assert.NotEmpty(_progress.Texts);
    }

    [Fact]
    public async Task Cancelling_in_the_middle_of_a_stream_ends_the_process()
    {
        _fake.Write(new { adapt = new { kind = "success", skillFile = "---\nname: docker\n---\nBody\n", chunkSize = 6, hangAfterChunks = 3 } });
        using var cancellation = new CancellationTokenSource();

        var task = AdaptAsync(cancellation.Token);
        await WaitUntilAsync(() => _progress.Texts.Any());
        await cancellation.CancelAsync();
        var result = await task;

        Assert.IsType<AdaptationFailure.Cancelled>(result.Error);
        await WaitUntilAsync(() => !_fake.IsRunning());
        Assert.False(_fake.IsRunning());
    }

    [Fact]
    public async Task Text_that_is_not_the_skill_file_is_never_reported()
    {
        _fake.Write(new { adapt = new { kind = "resultOnly", skillFile = "---\nname: docker\n---\n" } });

        await AdaptAsync();

        Assert.Empty(_progress.Reports);
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
