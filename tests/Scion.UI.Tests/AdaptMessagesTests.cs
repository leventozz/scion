using Scion.Application.Adapting;

namespace Scion.UI.Tests;

public class AdaptMessagesTests
{
    private static readonly AiProvider Claude = new(new AiProviderId("claude"), "Claude", "Claude Code", "Claude", new Uri("https://example.test/install"),
        new AiUsageNotes("Counts toward your Claude plan.", "Uses your Anthropic API key."));

    private static readonly AiProvider Codex = new(new AiProviderId("codex"), "Codex", "Codex CLI", "ChatGPT", new Uri("https://example.test/install-codex"),
        AiUsageNotes.Always("Counts toward your ChatGPT plan unless an API key is set for Codex."));

    [Theory]
    [InlineData(1, "1 KB")]
    [InlineData(1024, "1 KB")]
    [InlineData(1025, "2 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(3000, "3 KB")]
    public void Rounds_a_size_up_to_whole_kilobytes_with_a_least_one(int bytes, string expected)
    {
        Assert.Equal(expected, AdaptMessages.SizeOf(bytes));
    }

    [Fact]
    public void A_shared_item_with_a_size_shows_it_after_a_middle_dot_and_one_without_shows_only_its_label()
    {
        Assert.Equal("CLAUDE.md from Web · 3 KB", AdaptMessages.For(new SharedItem("CLAUDE.md from Web", "x", 2100)));
        Assert.Equal("Folder names in Web (two levels)", AdaptMessages.For(new SharedItem("Folder names in Web (two levels)", "x", null)));
    }

    [Fact]
    public void The_provider_line_says_who_is_connected_through_what_and_nothing_about_its_plan_or_privacy()
    {
        Assert.Equal("Claude · Connected via Claude Code", AdaptMessages.ProviderLine(Claude, AiProviderState.Ready));
    }

    [Theory]
    [InlineData(AiProviderState.NotInstalled, "Claude Code isn't installed.")]
    [InlineData(AiProviderState.SignedOut, "Sign in to Claude Code to continue.")]
    public void The_provider_line_names_what_is_missing(AiProviderState state, string expected)
    {
        Assert.Equal(expected, AdaptMessages.ProviderLine(Claude, state));
    }

    [Fact]
    public void The_sign_in_button_names_what_the_user_signs_in_with_not_the_provider()
    {
        Assert.Equal("Sign in with Claude", AdaptMessages.SignIn(Claude));
        Assert.Equal("Sign in with ChatGPT", AdaptMessages.SignIn(Codex));
    }

    [Theory]
    [InlineData(AiProviderState.Ready, "Connected via Codex CLI")]
    [InlineData(AiProviderState.SignedOut, "Sign in needed")]
    [InlineData(AiProviderState.NotInstalled, "Not installed")]
    public void A_row_of_the_provider_list_says_in_a_few_words_how_the_provider_is_doing(AiProviderState state, string expected) =>
        Assert.Equal(expected, AdaptMessages.ProviderState(state, Codex));

    [Theory]
    [InlineData(AiProviderState.Ready, "Connected via Codex CLI")]
    [InlineData(AiProviderState.SignedOut, "Sign in to Codex CLI to continue.")]
    [InlineData(AiProviderState.NotInstalled, "Codex CLI isn't installed.")]
    public void Beside_the_name_of_the_provider_the_line_does_not_say_the_name_again(AiProviderState state, string expected) =>
        Assert.Equal(expected, AdaptMessages.ProviderDetail(Codex, state));

    [Fact]
    public void A_file_the_client_loads_by_itself_is_listed_with_the_client_that_loads_it_and_its_size()
    {
        Assert.Equal(
            "~/.codex/AGENTS.md (loaded by Codex) · 2 KB",
            AdaptMessages.LoadedBy(new ProviderLoadedFile(".codex/AGENTS.md", 1500), Codex));
    }

    [Fact]
    public void Has_words_for_the_install_help_and_the_wait()
    {
        Assert.Equal("How to install", AdaptMessages.HowToInstall);
        Assert.Equal("Finish signing in in your browser…", AdaptMessages.WaitingForSignIn);
    }

    [Fact]
    public void Running_names_the_skill_and_the_target()
    {
        Assert.Equal("Adapting docker for Web…", AdaptMessages.Running("docker", "Web"));
    }

    [Fact]
    public void Tells_a_usage_limit_the_way_the_design_does()
    {
        Assert.Equal(
            "Claude Code couldn't finish: your usage limit was reached. Try again later.",
            AdaptMessages.For(new AdaptationFailure.UsageLimitReached(), Claude));
    }

    [Fact]
    public void Tells_a_time_out()
    {
        Assert.Equal(
            "Claude Code took too long, so Scion stopped it. Try again.",
            AdaptMessages.For(new AdaptationFailure.TimedOut(), Claude));
    }

    [Fact]
    public void Tells_what_claude_code_said_when_it_failed()
    {
        Assert.Equal(
            "Claude Code couldn't finish: The model is overloaded",
            AdaptMessages.For(new AdaptationFailure.Failed("The model is overloaded"), Claude));
    }

    [Fact]
    public void Has_words_for_every_failure_even_the_ones_that_send_the_user_back_to_the_form()
    {
        Assert.Equal("Claude Code isn't installed.", AdaptMessages.For(new AdaptationFailure.NotInstalled(), Claude));
        Assert.Equal("Sign in to Claude Code to continue.", AdaptMessages.For(new AdaptationFailure.SignedOut(), Claude));
        Assert.Equal("", AdaptMessages.For(new AdaptationFailure.Cancelled(), Claude));
    }

    [Theory]
    [InlineData(PrepareAdaptationError.SameSourceAndTarget, "Choose a different project or agent.")]
    [InlineData(PrepareAdaptationError.TargetFolderMissing, "This folder no longer exists.")]
    [InlineData(PrepareAdaptationError.SourceUnreadable, "Couldn't read SKILL.md.")]
    public void Tells_why_a_target_cannot_be_prepared(PrepareAdaptationError error, string expected)
    {
        Assert.Equal(expected, AdaptMessages.For(error));
    }

    [Theory]
    [InlineData(SaveAdaptedSkillError.Failed, "Couldn't save the skill.")]
    [InlineData(SaveAdaptedSkillError.TargetFolderMissing, "Couldn't save: the folder no longer exists.")]
    public void Tells_why_a_save_failed(SaveAdaptedSkillError error, string expected)
    {
        Assert.Equal(expected, AdaptMessages.For(error));
    }

    [Fact]
    public void A_summary_line_of_one_item_reads_like_that_item()
    {
        var item = new SharedItem("SKILL.md from docker", "x", 1500);

        Assert.Equal("SKILL.md from docker · 2 KB", AdaptMessages.For(new SharedSummaryLine.Item(item)));
    }

    [Theory]
    [InlineData(3, 7000, "3 project files · 7 KB")]
    [InlineData(1, 2000, "1 project file · 2 KB")]
    [InlineData(2, 10, "2 project files · 1 KB")]
    public void The_project_files_line_counts_the_files_and_sums_their_size(int count, int bytes, string expected)
    {
        Assert.Equal(expected, AdaptMessages.For(new SharedSummaryLine.ProjectFiles(count, bytes)));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(42, "0:42")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(-5, "0:00")]
    public void The_elapsed_time_is_minutes_and_two_digit_seconds(int seconds, string expected)
    {
        Assert.Equal(expected, AdaptMessages.Elapsed(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(32768, "Not shared (over 32 KB): big.json")]
    [InlineData(65536, "Not shared (over 64 KB): big.json")]
    public void A_file_over_the_limit_is_told_with_the_limit_that_applied(int limitBytes, string expected)
    {
        var omitted = new OmittedItem("big.json", new OmittedReason.OverFileLimit(limitBytes));

        Assert.Equal(expected, AdaptMessages.For(omitted));
    }

    [Fact]
    public void A_file_left_out_because_the_total_was_reached_is_told_so()
    {
        var omitted = new OmittedItem("late.json", new OmittedReason.TotalLimitReached());

        Assert.Equal("Not shared (total limit reached): late.json", AdaptMessages.For(omitted));
    }

    [Fact]
    public void Tells_that_the_skill_exists_in_the_target()
    {
        Assert.Equal("A skill with this name already exists in Web.", AdaptMessages.AlreadyExists("Web"));
    }
}
