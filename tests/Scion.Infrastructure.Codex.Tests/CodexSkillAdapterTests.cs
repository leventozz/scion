using Scion.Application.Adapting;
using Scion.Domain;

namespace Scion.Infrastructure.Codex.Tests;

[Collection("FakeCodex")]
public class CodexSkillAdapterTests : IDisposable
{
    private static readonly AdaptationPrompt Prompt = new("Return the skill.", "<<< SKILL.md from docker >>>\n# Docker — ünïcode\n");

    private static readonly string[] DisabledFeatures =
    [
        "shell_tool", "unified_exec", "view_image", "apps", "plugins", "memories", "multi_agent", "browser_use", "computer_use",
        "image_generation", "hooks", "skill_search", "code_mode_host", "sleep_tool", "tool_suggest",
    ];

    private readonly FakeCodex _fake = new(new { signedIn = true, exec = new { kind = "success", skillFile = "---\nname: docker\n---\n" } });
    private readonly IDisposable _environment;
    private readonly List<AdaptationProgress> _reports = [];

    public CodexSkillAdapterTests() => _environment = _fake.UseAsEnvironment();

    public void Dispose()
    {
        _environment.Dispose();
        _fake.Dispose();
    }

    private CodexSkillAdapter AdapterFor(string? executablePath = null)
    {
        var executable = new CodexExecutable(executablePath ?? _fake.ExecutablePath, "/unused", _ => null);

        return new CodexSkillAdapter(executable, new CodexStatus(executable));
    }

    private Task<Result<string, AdaptationFailure>> AdaptAsync(CancellationToken cancellationToken = default) =>
        AdapterFor().AdaptAsync(Prompt, new Progress<AdaptationProgress>(_reports.Add), cancellationToken);

    [Fact]
    public async Task Passes_exactly_the_flags_that_keep_codex_to_the_prompt()
    {
        await AdaptAsync();

        var call = Assert.Single(_fake.Calls());
        var expected = new List<string>
        {
            "exec", "-",
            "--json",
            "--ephemeral",
            "--ignore-user-config",
            "--ignore-rules",
            "--skip-git-repo-check",
            "--sandbox", "read-only",
            "-C", call.Cwd,
        };
        expected.AddRange(DisabledFeatures.SelectMany(feature => new[] { "--disable", feature }));
        expected.AddRange(["-c", "web_search=\"disabled\"", "--output-schema", call.SchemaPath!]);

        Assert.Equal(expected, call.Args);
    }

    [Fact]
    public async Task Never_asks_for_a_wider_sandbox_or_for_approval_to_be_skipped()
    {
        await AdaptAsync();

        var args = Assert.Single(_fake.Calls()).Args;
        Assert.DoesNotContain("--dangerously-bypass-approvals-and-sandbox", args);
        Assert.DoesNotContain("--full-auto", args);
        Assert.DoesNotContain("workspace-write", args);
        Assert.DoesNotContain("danger-full-access", args);
    }

    [Fact]
    public async Task Sends_the_instructions_and_then_the_documents_on_standard_input_and_nothing_else()
    {
        await AdaptAsync();

        Assert.Equal(Prompt.Instructions + "\n\n" + Prompt.Content, Assert.Single(_fake.Calls()).Stdin);
    }

    [Fact]
    public async Task Names_the_empty_folder_it_runs_in_as_its_working_folder()
    {
        await AdaptAsync();

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(0, call.CwdEntries);
        Assert.Equal(call.Cwd, call.WorkDirectory);
        Assert.False(Directory.Exists(call.Cwd));
    }

    [Fact]
    public async Task Gives_the_answer_schema_in_a_file_outside_the_working_folder_and_removes_it_afterwards()
    {
        await AdaptAsync();

        var call = Assert.Single(_fake.Calls());
        Assert.Equal(AdaptationPrompt.AnswerSchema, call.Schema);
        Assert.False(call.SchemaPath!.StartsWith(call.Cwd, StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(call.SchemaPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(call.SchemaPath)));
    }

    [Fact]
    public async Task Removes_the_schema_file_when_the_run_is_cancelled()
    {
        _fake.Write(new { exec = new { kind = "hang" } });
        using var cancellation = new CancellationTokenSource();

        var task = AdaptAsync(cancellation.Token);
        await _fake.WaitUntilStartedAsync();
        await cancellation.CancelAsync();
        await task;

        var call = Assert.Single(_fake.Calls());
        Assert.False(Directory.Exists(Path.GetDirectoryName(call.SchemaPath)));
    }

    [Fact]
    public async Task Leaves_the_environment_as_it_is_and_never_sets_an_api_key()
    {
        var previous = Environment.GetEnvironmentVariable("CODEX_API_KEY");
        Environment.SetEnvironmentVariable("CODEX_API_KEY", null);
        try
        {
            await AdaptAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_API_KEY", previous);
        }

        Assert.False(Assert.Single(_fake.Calls()).ApiKeySet);
    }

    [Fact]
    public async Task Gives_the_skill_file_of_the_last_agent_message()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "success", skillFile = "---\nname: docker\n---\nBody — ünïcode \U0001F600\n" } });

        var result = await AdaptAsync();

        Assert.Equal("---\nname: docker\n---\nBody — ünïcode \U0001F600\n", result.Value);
    }

    [Fact]
    public async Task Reports_no_progress_because_codex_does_not_stream_the_answer()
    {
        await AdaptAsync();

        Assert.Empty(_reports);
    }

    [Fact]
    public async Task The_warning_that_code_mode_is_unavailable_does_not_stop_the_run()
    {
        var result = await AdaptAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_tool_that_was_blocked_leaves_the_result_alone()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "toolBlocked", skillFile = "---\nname: docker\n---\n" } });

        var result = await AdaptAsync();

        Assert.Equal("---\nname: docker\n---\n", result.Value);
    }

    [Fact]
    public async Task A_tool_that_ran_ends_the_process_and_the_result_is_thrown_away()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "toolRan" } });

        var result = await AdaptAsync();

        Assert.Equal(
            new AdaptationFailure.Failed("Codex used a tool, so Scion stopped it and discarded the result."), result.Error);
        await WaitUntilAsync(() => !_fake.IsRunning());
        Assert.False(_fake.IsRunning());
    }

    [Fact]
    public async Task A_usage_limit_is_told_apart()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "usageLimit" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.UsageLimitReached>(result.Error);
    }

    [Fact]
    public async Task A_failed_turn_while_signed_out_is_signed_out()
    {
        _fake.Write(new { signedIn = false, exec = new { kind = "turnFailed", message = "401 Unauthorized" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.SignedOut>(result.Error);
        Assert.Equal(["login", "status"], _fake.Calls().Last().Args);
    }

    [Fact]
    public async Task A_failed_turn_while_signed_in_is_a_failure_with_the_message()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "turnFailed", message = "The model is overloaded" } });

        var result = await AdaptAsync();

        Assert.Equal(new AdaptationFailure.Failed("The model is overloaded"), result.Error);
    }

    [Fact]
    public async Task A_message_that_is_not_the_skill_file_is_a_failure()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "notJson", message = "I cannot do that." } });

        var result = await AdaptAsync();

        Assert.Equal(new AdaptationFailure.Failed("Codex returned no skill file."), result.Error);
    }

    [Fact]
    public async Task An_empty_skill_file_is_a_failure()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "success", skillFile = "" } });

        var result = await AdaptAsync();

        Assert.IsType<AdaptationFailure.Failed>(result.Error);
    }

    [Fact]
    public async Task Ending_with_an_error_and_no_answer_shows_the_first_line_of_what_codex_wrote_on_standard_error()
    {
        _fake.Write(new { signedIn = true, exec = new { kind = "crashWithoutAnswer", message = "Error: something broke\nmore detail" } });

        var result = await AdaptAsync();

        Assert.Equal(new AdaptationFailure.Failed("Error: something broke"), result.Error);
    }

    [Fact]
    public async Task A_missing_executable_is_not_installed()
    {
        var result = await AdapterFor(Path.Combine(Path.GetTempPath(), "scion-tests", "no-such-codex"))
            .AdaptAsync(Prompt, new Progress<AdaptationProgress>(), CancellationToken.None);

        Assert.IsType<AdaptationFailure.NotInstalled>(result.Error);
    }

    [Fact]
    public async Task Cancelling_ends_the_process_and_is_cancelled()
    {
        _fake.Write(new { exec = new { kind = "hang" } });
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
