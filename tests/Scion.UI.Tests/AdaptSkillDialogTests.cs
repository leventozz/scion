using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

public class AdaptSkillDialogTests : BunitContext
{
    private const string SourceText = "---\nname: docker\ndescription: Runs compose files.\n---\n# Docker\nUses MongoDB.\n";

    private static readonly Location ApiLocation = new Location.Project("/work/api");
    private static readonly CopyPlace Api = new("api", ApiLocation);
    private static readonly SkillSummary Docker = new("docker", "docker", "Runs compose files.", [], CanBeDeleted: true);

    private readonly UiFakes _fakes = new();
    private int _closed;
    private readonly List<SkillCopyOutcome> _saved = [];

    public AdaptSkillDialogTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        DefaultWaitTimeout = TimeSpan.FromSeconds(10); // a loaded machine runs the async steps later, not differently
        _fakes.SkillFiles.Add(ApiLocation, Agent.ClaudeCode, "docker", "/work/api/.claude/skills/docker", ("SKILL.md", SourceText));
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", UiFakes.Noon));
        _fakes.TargetContext.AddInstructionFile("CLAUDE.md", new string('x', 2100));
        _fakes.TargetContext.AddManifest("web.csproj", "<Project/>");
        _fakes.TargetContext.AddFolderNames("src", "src/Web");
    }

    private IRenderedComponent<AdaptSkillDialog> Render(Agent agent = Agent.ClaudeCode) =>
        Render<AdaptSkillDialog>(parameters => parameters
            .Add(dialog => dialog.Source, Api)
            .Add(dialog => dialog.SourceAgent, agent)
            .Add(dialog => dialog.Skill, Docker)
            .Add(dialog => dialog.OnClose, () => _closed++)
            .Add(dialog => dialog.OnSaved, (SkillCopyOutcome outcome) => _saved.Add(outcome)));

    private IRenderedComponent<AdaptSkillDialog> RenderWithTarget()
    {
        var dialog = Render();
        ChooseWeb(dialog);

        return dialog;
    }

    private static void ChooseWeb(IRenderedComponent<AdaptSkillDialog> dialog, int expectedRows = 3)
    {
        dialog.Find(".place-picker-button").Click();
        dialog.FindAll(".place-option")
            .Single(option => option.QuerySelector(".place-option-name")!.TextContent == "web")
            .Click();
        dialog.WaitForAssertion(() => Assert.Equal(expectedRows, dialog.FindAll(".shared-list li").Count));
    }

    private static IElement Button(IRenderedComponent<AdaptSkillDialog> dialog, string text) =>
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private static string[] SharedItems(IRenderedComponent<AdaptSkillDialog> dialog) =>
        dialog.FindAll(".shared-list li").Select(item => item.TextContent.Trim()).ToArray();

    private void Adapt(IRenderedComponent<AdaptSkillDialog> dialog)
    {
        Button(dialog, "Adapt").Click();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-adapt-review")));
    }

    // ---- the provider

    [Fact]
    public void The_dialog_uses_the_provider_the_catalog_offers_for_its_state_its_note_and_the_adaptation()
    {
        var other = new AiProvider(
            new AiProviderId("other"), "Other", "Other CLI", "Other", new Uri("https://example.test/install-other"),
            new AiUsageNotes("Counts toward the other plan.", "Uses the other key."));
        var otherAdapter = new FakeSkillAdapter();
        _fakes.AddProviderTo(Services, other, new FakeAiProviderStatus(), otherAdapter);

        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Other · Connected via Other CLI", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.Equal("Counts toward the other plan.", dialog.Find(".adapt-provider-note").TextContent.Trim());
        Adapt(dialog);
        Assert.Single(otherAdapter.Prompts);
        Assert.Empty(_fakes.SkillAdapter.Prompts);
    }

    // ---- the form

    [Fact]
    public void Is_a_modal_dialog_with_a_close_button_and_names_the_source_and_asks_for_a_target()
    {
        var dialog = Render();

        Assert.Equal("true", dialog.Find("[role=dialog]").GetAttribute("aria-modal"));
        Assert.Equal("Adapt with AI", dialog.Find("h2").TextContent);
        Assert.NotNull(dialog.Find("button[aria-label=Close]"));
        Assert.Equal("api · Claude · docker", dialog.Find(".copy-fixed").TextContent);
        Assert.Equal("Choose a project", dialog.Find(".place-picker-button").TextContent.Trim());
        Assert.Equal(["Claude", "Codex"], dialog.FindAll(".agent-switch button").Select(button => button.TextContent));
    }

    [Fact]
    public void Shows_no_shared_list_and_cannot_adapt_before_a_target_is_chosen()
    {
        var dialog = Render();

        Assert.Empty(dialog.FindAll(".shared-box"));
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));
    }

    [Fact]
    public void The_target_agent_starts_as_the_agent_of_the_source()
    {
        var dialog = Render(Agent.Codex);

        Assert.Equal("true", dialog.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Sums_up_what_will_be_shared_once_a_target_is_chosen()
    {
        var dialog = RenderWithTarget();

        Assert.Equal("What will be shared", dialog.Find(".shared-title").TextContent);
        Assert.Equal(
            [
                "SKILL.md from docker · 1 KB",
                "2 project files · 3 KB",
                "Folder names in web (two levels)",
            ],
            SharedItems(dialog));
        Assert.Equal("Source code is never shared. Scripts and references are copied unchanged.", dialog.Find(".shared-note").TextContent);
    }

    private static string[] NotShared(IRenderedComponent<AdaptSkillDialog> dialog) =>
        dialog.FindAll(".shared-omitted li").Select(item => item.TextContent.Trim()).ToArray();

    [Fact]
    public void Nothing_is_listed_as_not_shared_when_everything_fits()
    {
        var dialog = RenderWithTarget();

        Assert.Empty(dialog.FindAll(".shared-omitted"));
    }

    [Fact]
    public void A_file_over_the_limit_is_listed_as_not_shared_apart_from_the_shared_items_and_is_not_sent()
    {
        _fakes.TargetContext.AddManifest("big.json", new string('q', 33 * 1024));
        var dialog = Render();
        ChooseWeb(dialog);

        Assert.Equal(["Not shared (over 32 KB): big.json"], NotShared(dialog));
        Assert.DoesNotContain(SharedItems(dialog), line => line.Contains("big.json"));
        dialog.Find(".shared-toggle").Click();
        Assert.DoesNotContain(SharedItems(dialog), line => line.Contains("big.json"));
        Assert.Equal(["Not shared (over 32 KB): big.json"], NotShared(dialog));

        Adapt(dialog);

        var prompt = Assert.Single(_fakes.SkillAdapter.Prompts).Content;
        Assert.DoesNotContain("big.json", prompt);
        Assert.DoesNotContain("qqqqqqqq", prompt);
    }

    [Fact]
    public void Files_left_out_because_the_total_was_reached_are_listed_the_same_way()
    {
        foreach (var name in new[] { "a.json", "b.json", "c.json", "d.json", "e.json" })
        {
            _fakes.TargetContext.AddManifest(name, new string(name[0], 30 * 1024));
        }

        var dialog = Render();
        ChooseWeb(dialog);

        Assert.Equal(["Not shared (total limit reached): e.json"], NotShared(dialog));
        Assert.DoesNotContain(SharedItems(dialog), line => line.Contains("e.json"));
    }

    [Fact]
    public void What_the_form_lists_as_shared_never_counts_what_is_not_shared()
    {
        _fakes.TargetContext.AddManifest("big.json", new string('q', 33 * 1024));
        var dialog = Render();
        ChooseWeb(dialog);
        dialog.Find(".shared-toggle").Click();
        var listed = dialog.FindAll(".shared-list li").Select(item => item.TextContent.Trim().Split(" · ")[0]).ToArray();

        Adapt(dialog);

        var prompt = Assert.Single(_fakes.SkillAdapter.Prompts).Content;
        var titles = prompt.Split('\n').Where(line => line.StartsWith("<<< ") && line.EndsWith(" >>>")).Select(line => line[4..^4]);
        Assert.Equal(listed, titles);
        Assert.DoesNotContain("big.json", listed);
        Assert.DoesNotContain("big.json", titles);
    }

    [Fact]
    public void Show_all_lists_every_shared_item_and_Show_less_goes_back_to_the_summary()
    {
        var dialog = RenderWithTarget();

        Assert.Equal("Show all", dialog.Find(".shared-toggle").TextContent.Trim());
        dialog.Find(".shared-toggle").Click();

        Assert.Equal(
            [
                "SKILL.md from docker · 1 KB",
                "CLAUDE.md from web · 3 KB",
                "web.csproj · 1 KB",
                "Folder names in web (two levels)",
            ],
            SharedItems(dialog));
        Assert.Equal("Show less", dialog.Find(".shared-toggle").TextContent.Trim());

        dialog.Find(".shared-toggle").Click();

        Assert.Equal(3, SharedItems(dialog).Length);
    }

    [Fact]
    public void There_is_no_Show_all_when_the_summary_is_the_whole_list()
    {
        _fakes.TargetContext.Clear();
        var dialog = Render();
        ChooseWeb(dialog, expectedRows: 1);

        Assert.Equal(["SKILL.md from docker · 1 KB"], SharedItems(dialog));
        Assert.Empty(dialog.FindAll(".shared-toggle"));
    }

    [Fact]
    public void Extra_instructions_are_the_fourth_line_of_the_summary()
    {
        var dialog = RenderWithTarget();

        dialog.Find("textarea.adapt-instructions").Input("We use PostgreSQL.");

        Assert.Equal(
            [
                "SKILL.md from docker · 1 KB",
                "2 project files · 3 KB",
                "Folder names in web (two levels)",
                "Your extra instructions",
            ],
            SharedItems(dialog));
    }

    [Fact]
    public void Extra_instructions_appear_in_the_shared_list_and_are_sent()
    {
        var dialog = RenderWithTarget();

        dialog.Find("textarea.adapt-instructions").Input("We use PostgreSQL.");
        Assert.Equal("Your extra instructions", SharedItems(dialog).Last());
        Adapt(dialog);

        Assert.Contains("We use PostgreSQL.", Assert.Single(_fakes.SkillAdapter.Prompts).Content);
    }

    [Fact]
    public void What_the_form_lists_is_exactly_what_the_prompt_is_titled_with()
    {
        var dialog = RenderWithTarget();
        dialog.Find("textarea.adapt-instructions").Input("We use PostgreSQL.");
        dialog.Find(".shared-toggle").Click();
        var listed = dialog.FindAll(".shared-list li").Select(item => item.TextContent.Trim().Split(" · ")[0]).ToArray();

        Adapt(dialog);

        var prompt = Assert.Single(_fakes.SkillAdapter.Prompts);
        var titles = prompt.Content.Split('\n').Where(line => line.StartsWith("<<< ") && line.EndsWith(" >>>")).Select(line => line[4..^4]);
        Assert.Equal(listed, titles);
    }

    [Fact]
    public void Says_why_a_target_cannot_be_used_and_cannot_adapt()
    {
        var dialog = Render();
        dialog.Find(".place-picker-button").Click();
        dialog.FindAll(".place-option").Single(option => option.QuerySelector(".place-option-name")!.TextContent == "web").Click();
        dialog.FindAll(".agent-switch button").Single(button => button.TextContent == "Claude").Click();
        _fakes.Folders.MarkMissing("/work/web");
        dialog.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();

        dialog.WaitForAssertion(() => Assert.Equal("This folder no longer exists.", dialog.Find(".target-problem").TextContent));
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));
        Assert.Empty(dialog.FindAll(".shared-box"));
    }

    [Fact]
    public void Refuses_the_same_agent_in_the_same_place()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/api", "api", UiFakes.Noon));
        var dialog = Render();
        dialog.Find(".place-picker-button").Click();
        dialog.FindAll(".place-option").Single(option => option.QuerySelector(".place-option-name")!.TextContent == "api").Click();

        dialog.WaitForAssertion(() => Assert.Equal("Choose a different project or agent.", dialog.Find(".target-problem").TextContent));
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));
    }

    // ---- the provider

    [Fact]
    public void A_ready_provider_is_named_in_a_plain_line_with_the_usage_note_below_it_and_adapt_is_enabled()
    {
        var dialog = RenderWithTarget();

        Assert.Equal("Claude · Connected via Claude Code", dialog.Find(".adapt-provider-line").TextContent.Trim());
        Assert.Equal("Counts toward your Claude plan.", dialog.Find(".adapt-provider-note").TextContent.Trim());
        Assert.False(Button(dialog, "Adapt").HasAttribute("disabled"));
        Assert.Empty(dialog.FindAll(".provider-action"));
    }

    [Fact]
    public void Working_with_an_API_key_the_note_says_so()
    {
        _fakes.ProviderStatus.UsageBasis = AiUsageBasis.ApiKey;
        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Uses your Anthropic API key.", dialog.Find(".adapt-provider-note").TextContent.Trim()));
        Assert.DoesNotContain("plan", dialog.Find(".adapt-provider").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void When_the_client_does_not_say_how_it_is_signed_in_there_is_no_note_at_all()
    {
        _fakes.ProviderStatus.UsageBasis = AiUsageBasis.Unknown;
        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Claude · Connected via Claude Code", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.Empty(dialog.FindAll(".adapt-provider-note"));
        Assert.DoesNotContain("plan", dialog.Find(".adapt-provider").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API key", dialog.Find(".adapt-provider").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void After_signing_in_the_note_follows_the_new_sign_in()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;
        _fakes.ProviderStatus.UsageBasis = AiUsageBasis.ApiKey;
        var dialog = RenderWithTarget();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".provider-action")));
        dialog.Find(".provider-action").Click();
        Assert.Empty(dialog.FindAll(".adapt-provider-note"));

        _fakes.ProviderStatus.UsageBasis = AiUsageBasis.Subscription;
        _fakes.ProviderStatus.FinishSignIn();
        _fakes.Time.Advance(TimeSpan.FromSeconds(2));

        dialog.WaitForAssertion(() => Assert.Equal("Counts toward your Claude plan.", dialog.Find(".adapt-provider-note").TextContent.Trim()));
    }

    [Fact]
    public void The_footer_has_the_provider_on_the_left_and_cancel_and_adapt_on_the_right()
    {
        var dialog = RenderWithTarget();

        var footer = dialog.Find(".dialog-footer");

        Assert.Equal(
            ["adapt-provider", "button button-secondary", "button button-primary"],
            footer.Children.Select(child => child.ClassName!).ToArray());
    }

    [Fact]
    public void Never_makes_claims_about_privacy_or_security_or_a_sign_out()
    {
        var dialog = RenderWithTarget();

        var text = dialog.Markup;

        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Without_a_ready_provider_there_is_no_usage_note()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;
        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Sign in to Claude Code to continue.", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.Empty(dialog.FindAll(".adapt-provider-note"));
    }

    [Fact]
    public void Without_claude_code_it_says_so_offers_how_to_install_and_cannot_adapt()
    {
        _fakes.ProviderStatus.State = AiProviderState.NotInstalled;
        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Claude Code isn't installed.", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));

        Button(dialog, "How to install").Click();

        Assert.Equal([UiFakes.Provider.InstallHelp], _fakes.Links.Opened);
    }

    [Fact]
    public void When_signed_out_it_offers_to_sign_in_and_cannot_adapt()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;
        var dialog = RenderWithTarget();

        dialog.WaitForAssertion(() => Assert.Equal("Sign in to Claude Code to continue.", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.Equal("Sign in with Claude", dialog.Find(".provider-action").TextContent.Trim());
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));
    }

    [Fact]
    public void Signing_in_waits_for_the_browser_and_becomes_ready_when_claude_code_is()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;
        var dialog = RenderWithTarget();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".provider-action")));

        dialog.Find(".provider-action").Click();

        Assert.Equal("Finish signing in in your browser…", dialog.Find(".adapt-provider-line").TextContent.Trim());
        Assert.Equal("Cancel", dialog.Find(".adapt-provider .text-action").TextContent.Trim());
        Assert.True(Button(dialog, "Adapt").HasAttribute("disabled"));
        Assert.Equal(1, _fakes.ProviderStatus.SignInsStarted);

        _fakes.ProviderStatus.FinishSignIn();
        _fakes.Time.Advance(TimeSpan.FromSeconds(2));

        dialog.WaitForAssertion(() => Assert.Equal("Claude · Connected via Claude Code", dialog.Find(".adapt-provider-line").TextContent.Trim()));
        Assert.False(Button(dialog, "Adapt").HasAttribute("disabled"));
    }

    [Fact]
    public void Cancelling_the_wait_ends_the_sign_in_and_offers_it_again()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;
        var dialog = RenderWithTarget();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".provider-action")));
        dialog.Find(".provider-action").Click();

        dialog.Find(".adapt-provider .text-action").Click();

        dialog.WaitForAssertion(() => Assert.Equal("Sign in with Claude", dialog.Find(".provider-action").TextContent.Trim()));
        Assert.True(_fakes.ProviderStatus.SignInCancelled);
    }

    // ---- running

    [Fact]
    public void Adapting_shows_what_is_going_on_the_time_so_far_an_endless_bar_an_empty_text_box_and_only_a_cancel_button()
    {
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        Assert.Equal("Adapting docker for web…", dialog.Find(".adapt-running-text").TextContent);
        Assert.Equal("0:00", dialog.Find(".adapt-elapsed").TextContent);
        Assert.NotNull(dialog.Find("[role=progressbar]"));
        Assert.Equal("", dialog.Find(".streaming-text").Own());
        Assert.NotNull(dialog.Find(".streaming-empty .sparkle"));
        Assert.Equal(["Cancel"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void The_time_so_far_counts_up()
    {
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();
        Button(dialog, "Adapt").Click();

        _fakes.Time.Advance(TimeSpan.FromSeconds(42));

        dialog.WaitForAssertion(() => Assert.Equal("0:42", dialog.Find(".adapt-elapsed").TextContent));
    }

    [Fact]
    public void The_text_box_shows_the_skill_file_as_the_pieces_arrive_while_the_adaptation_still_runs()
    {
        _fakes.SkillAdapter.ReportsBeforeAnswering(
            new AdaptationProgress.TextArrived("---\nname: docker\n"),
            new AdaptationProgress.TextArrived("description: Run"));
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Equal("---\nname: docker\ndescription: Run", dialog.Find(".streaming-text").Own()));
        Assert.Empty(dialog.FindAll(".streaming-empty"));
        Assert.NotNull(dialog.Find(".streaming-text .streaming-cursor .sparkle"));
        Assert.NotEmpty(dialog.FindAll(".adapt-running-text"));
        Assert.Empty(dialog.FindAll(".dialog-adapt-review"));
    }

    [Fact]
    public void The_text_box_scrolls_to_the_end_when_text_arrives()
    {
        _fakes.SkillAdapter.ReportsBeforeAnswering(new AdaptationProgress.TextArrived("line"));
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, call => call.Identifier == "scion.scrollToEnd"));
    }

    [Fact]
    public void Starting_over_empties_the_text_box()
    {
        _fakes.SkillAdapter.ReportsBeforeAnswering(
            new AdaptationProgress.TextArrived("first try"),
            new AdaptationProgress.StartedOver(),
            new AdaptationProgress.TextArrived("second"));
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Equal("second", dialog.Find(".streaming-text").Own()));
    }

    [Fact]
    public void When_the_adaptation_is_done_the_review_shows_the_returned_text_not_the_streamed_one()
    {
        _fakes.SkillAdapter.ReportsBeforeAnswering(new AdaptationProgress.TextArrived("streamed only, never kept"));
        _fakes.SkillAdapter.Returns("---\nname: other\ndescription: Runs compose files.\n---\n# Docker\nFinal.\n");
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Empty(dialog.FindAll(".streaming-text"));
        Assert.Contains("Final.", dialog.Find(".diff").TextContent);
        Assert.DoesNotContain("streamed only", dialog.Markup);
    }

    [Fact]
    public void Every_step_has_a_fixed_header_a_scrolling_body_and_a_fixed_footer()
    {
        var gate = _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();

        AssertThreeParts(dialog);

        Button(dialog, "Adapt").Click();
        AssertThreeParts(dialog);

        gate.SetResult();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-adapt-review")));
        AssertThreeParts(dialog);
    }

    [Fact]
    public void A_failed_adaptation_has_the_same_three_parts()
    {
        _fakes.SkillAdapter.FailsWith(new AdaptationFailure.Failed("The model is overloaded"));
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-footer-message")));
        AssertThreeParts(dialog);
    }

    private static void AssertThreeParts(IRenderedComponent<AdaptSkillDialog> dialog) =>
        Assert.Equal(
            ["dialog-header", "dialog-body", "dialog-footer"],
            dialog.Find(".dialog-adapt").Children.Select(child => child.ClassList.First(name => name.StartsWith("dialog-") && name.Length > 7 && !name.StartsWith("dialog-footer-") && !name.StartsWith("dialog-adapt"))).ToArray());

    [Fact]
    public void Cancel_the_close_button_and_Esc_each_stop_the_adaptation_and_return_to_the_form_with_the_entries()
    {
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();
        dialog.Find("textarea.adapt-instructions").Input("Keep it short.");

        foreach (var stop in new Action[]
        {
            () => Button(dialog, "Cancel").Click(),
            () => dialog.Find("button[aria-label=Close]").Click(),
            () => dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" }),
        })
        {
            Button(dialog, "Adapt").Click();
            Assert.NotEmpty(dialog.FindAll(".adapt-running-text"));

            stop();

            dialog.WaitForAssertion(() => Assert.Empty(dialog.FindAll(".adapt-running-text")));
            Assert.True(_fakes.SkillAdapter.WasCancelled);
            Assert.Equal("Keep it short.", dialog.Find("textarea.adapt-instructions").GetAttribute("value"));
            Assert.Equal(0, _closed);
        }
    }

    [Fact]
    public void Closing_the_dialog_while_it_runs_cancels_the_adaptation()
    {
        _fakes.SkillAdapter.Hold();
        var dialog = RenderWithTarget();
        Button(dialog, "Adapt").Click();

        dialog.Instance.Dispose();

        Assert.True(SpinWait.SpinUntil(() => _fakes.SkillAdapter.WasCancelled, TimeSpan.FromSeconds(5)));
    }

    // ---- the review

    [Fact]
    public void Shows_the_original_and_the_adapted_skill_side_by_side_with_the_names_of_both_places()
    {
        _fakes.SkillAdapter.Returns("---\nname: other\ndescription: Runs compose files.\n---\n# Docker\nUses PostgreSQL.\n");
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Equal("Review the adapted skill", dialog.Find("h2").TextContent);
        Assert.Equal(["Original · api", "Adapted · web"], dialog.FindAll(".diff-title").Select(title => title.TextContent.Trim()));
        var changed = dialog.FindAll(".diff-row").Single(row => row.QuerySelector(".diff-removed") is not null);
        Assert.Equal("Uses MongoDB.", changed.QuerySelector(".diff-removed .diff-text")!.TextContent);
        Assert.Equal("Uses PostgreSQL.", changed.QuerySelector(".diff-added .diff-text")!.TextContent);
        Assert.Equal("−", changed.QuerySelector(".diff-removed .diff-mark")!.TextContent);
        Assert.Equal("+", changed.QuerySelector(".diff-added .diff-mark")!.TextContent);
    }

    [Fact]
    public void Forces_the_name_of_the_source_skill_and_shows_a_clean_header_without_warnings()
    {
        _fakes.SkillAdapter.Returns("---\nname: renamed-by-claude\ndescription: Runs compose files.\n---\nBody\n");
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Contains("name: docker", dialog.Find(".diff").TextContent);
        Assert.DoesNotContain("renamed-by-claude", dialog.Find(".diff").TextContent);
        Assert.Empty(dialog.FindAll(".review-warning"));
    }

    [Fact]
    public void Shows_the_usual_amber_warnings_of_the_adapted_header()
    {
        _fakes.SkillAdapter.Returns("---\nname: docker\n---\nBody\n");
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Equal(["Description is missing."], dialog.FindAll(".review-warning").Select(warning => warning.TextContent.Trim()));
    }

    [Fact]
    public void Offers_try_again_edit_before_saving_and_the_one_primary_save_to_the_target()
    {
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Equal(["Try again", "Edit before saving", "Save to web"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
        Assert.Equal(["Save to web"], dialog.FindAll(".dialog-footer .button-primary").Select(button => button.TextContent.Trim()));
        Assert.Equal("Scripts and references will be copied unchanged.", dialog.Find(".review-note").TextContent);
    }

    [Fact]
    public void Save_writes_the_adapted_skill_tells_the_screen_and_closes()
    {
        var dialog = RenderWithTarget();
        Adapt(dialog);

        Button(dialog, "Save to web").Click();

        dialog.WaitForAssertion(() => Assert.Equal([SkillCopyOutcome.Copied], _saved));
        var written = Assert.Single(_fakes.AdaptedSkillWriter.Written);
        Assert.Equal(("docker", false), (written.FolderName, written.Replaced));
        Assert.Contains("name: docker", written.SkillFile);
        Assert.Equal(1, _closed);
    }

    [Fact]
    public void A_skill_with_the_same_name_in_the_target_says_so_and_only_an_explicit_overwrite_replaces_it()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Mine."));
        var dialog = RenderWithTarget();

        Adapt(dialog);

        Assert.Equal("A skill with this name already exists in web.", dialog.Find(".review-problem").TextContent.Trim());
        Assert.Equal(["Overwrite in web"], dialog.FindAll(".dialog-footer .button-primary").Select(button => button.TextContent.Trim()));
        Assert.Empty(_fakes.AdaptedSkillWriter.Written);

        Button(dialog, "Overwrite in web").Click();

        dialog.WaitForAssertion(() => Assert.Equal([SkillCopyOutcome.Overwritten], _saved));
        Assert.True(Assert.Single(_fakes.AdaptedSkillWriter.Written).Replaced);
    }

    [Fact]
    public void A_skill_that_appears_during_the_review_is_not_overwritten_but_asks_again()
    {
        var dialog = RenderWithTarget();
        Adapt(dialog);
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Mine."));

        Button(dialog, "Save to web").Click();

        dialog.WaitForAssertion(() => Assert.Equal("A skill with this name already exists in web.", dialog.Find(".review-problem").TextContent.Trim()));
        Assert.Empty(_fakes.AdaptedSkillWriter.Written);
        Assert.NotNull(Button(dialog, "Overwrite in web"));
        Assert.Empty(_saved);
    }

    [Fact]
    public void A_failed_save_says_so_next_to_the_buttons_and_keeps_the_review()
    {
        var dialog = RenderWithTarget();
        Adapt(dialog);
        _fakes.AdaptedSkillWriter.Fails();

        Button(dialog, "Save to web").Click();

        dialog.WaitForAssertion(() => Assert.Equal("Couldn't save the skill.", dialog.Find(".dialog-footer .field-problem").TextContent.Trim()));
        Assert.Empty(_saved);
        Assert.Equal(0, _closed);
    }

    [Fact]
    public void Try_again_goes_back_to_the_form_with_the_target_and_the_instructions_kept()
    {
        var dialog = RenderWithTarget();
        dialog.Find("textarea.adapt-instructions").Input("Keep it short.");
        Adapt(dialog);

        Button(dialog, "Try again").Click();

        Assert.Equal("Keep it short.", dialog.Find("textarea.adapt-instructions").GetAttribute("value"));
        Assert.Equal("web", dialog.Find(".place-picker-button .place-name").TextContent);
        Assert.False(Button(dialog, "Adapt").HasAttribute("disabled"));
    }

    [Fact]
    public void Edit_before_saving_turns_the_adapted_side_into_an_editor_and_saves_what_the_user_wrote()
    {
        var dialog = RenderWithTarget();
        Adapt(dialog);

        Button(dialog, "Edit before saving").Click();
        dialog.Find("textarea.text-editor-input").Input("---\nname: docker\ndescription: Mine.\n---\nMy own text.\n");
        Button(dialog, "Save to web").Click();

        dialog.WaitForAssertion(() => Assert.Single(_fakes.AdaptedSkillWriter.Written));
        Assert.Equal("---\nname: docker\ndescription: Mine.\n---\nMy own text.\n", _fakes.AdaptedSkillWriter.Written[0].SkillFile);
        Assert.DoesNotContain(dialog.FindAll("button"), button => button.TextContent.Trim() == "Edit before saving");
    }

    [Fact]
    public void While_editing_the_warnings_follow_the_draft_after_a_short_pause()
    {
        var dialog = RenderWithTarget();
        Adapt(dialog);
        Button(dialog, "Edit before saving").Click();
        Assert.Empty(dialog.FindAll(".review-warning"));

        dialog.Find("textarea.text-editor-input").Input("---\nname: docker\n---\nBody\n");
        Assert.Empty(dialog.FindAll(".review-warning"));
        _fakes.Time.Advance(DraftHeaderCheck.Pause);

        dialog.WaitForAssertion(() => Assert.Equal(["Description is missing."], dialog.FindAll(".review-warning").Select(warning => warning.TextContent.Trim())));
    }

    // ---- failures

    [Fact]
    public void A_usage_limit_is_an_amber_line_next_to_Close_and_Try_again()
    {
        _fakes.SkillAdapter.FailsWith(new AdaptationFailure.UsageLimitReached());
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Equal(
            "Claude Code couldn't finish: your usage limit was reached. Try again later.",
            dialog.Find(".dialog-footer .field-problem").TextContent.Trim()));
        Assert.NotNull(dialog.Find(".dialog-footer .field-problem .warning-icon"));
        Assert.Equal(["Close", "Try again"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
        Assert.Empty(dialog.FindAll(".dialog-footer .button-primary"));
    }

    [Fact]
    public void Try_again_after_a_failure_goes_back_to_the_form_and_Close_closes()
    {
        _fakes.SkillAdapter.FailsWith(new AdaptationFailure.Failed("It broke"));
        var dialog = RenderWithTarget();
        Button(dialog, "Adapt").Click();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-footer .field-problem")));

        Button(dialog, "Try again").Click();
        Assert.False(Button(dialog, "Adapt").HasAttribute("disabled"));
        _fakes.SkillAdapter.FailsWith(new AdaptationFailure.TimedOut());
        Button(dialog, "Adapt").Click();
        dialog.WaitForAssertion(() => Assert.Equal("Claude Code took too long, so Scion stopped it. Try again.", dialog.Find(".dialog-footer .field-problem").TextContent.Trim()));
        Button(dialog, "Close").Click();

        Assert.Equal(1, _closed);
    }

    [Fact]
    public void A_provider_that_turns_out_signed_out_sends_the_user_back_to_the_form_to_sign_in()
    {
        _fakes.SkillAdapter.FailsWith(new AdaptationFailure.SignedOut());
        var dialog = RenderWithTarget();

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Equal("Sign in with Claude", dialog.Find(".provider-action").TextContent.Trim()));
        Assert.Empty(dialog.FindAll(".dialog-footer .field-problem"));
    }

    // ---- closing

    [Fact]
    public void The_close_button_and_Esc_close_the_form_and_a_click_on_the_backdrop_does_not()
    {
        var dialog = Render();

        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());
        dialog.Find("button[aria-label=Close]").Click();
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Button(dialog, "Cancel").Click();

        Assert.Equal(3, _closed);
    }

    [Fact]
    public void Puts_the_focus_on_the_dialog()
    {
        var dialog = Render();

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(dialog.Find("[role=dialog]"));
    }
}
