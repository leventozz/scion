using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Adapting;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>
/// Who adapts is chosen apart from where the skill goes: the provider picker never changes the target agent, the target place
/// or what will be shared (the file the provider's client loads by itself is listed, never sent).
/// </summary>
public class AdaptSkillDialogProviderTests : BunitContext
{
    private const string SourceText = "---\nname: docker\ndescription: Runs compose files.\n---\n# Docker\nUses MongoDB.\n";

    private static readonly Location ApiLocation = new Location.Project("/work/api");
    private static readonly CopyPlace Api = new("api", ApiLocation);
    private static readonly SkillSummary Docker = new("docker", "docker", "Runs compose files.", [], CanBeDeleted: true);

    private readonly UiFakes _fakes = new();
    private int _closed;
    private readonly List<SkillCopyOutcome> _saved = [];

    public AdaptSkillDialogProviderTests()
    {
        _fakes.AddTo(Services);
        _fakes.AddBothProvidersTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        DefaultWaitTimeout = TimeSpan.FromSeconds(10);
        _fakes.SkillFiles.Add(ApiLocation, Agent.ClaudeCode, "docker", "/work/api/.claude/skills/docker", ("SKILL.md", SourceText));
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", UiFakes.Noon));
        _fakes.TargetContext.AddInstructionFile("CLAUDE.md", new string('x', 2100));
        _fakes.TargetContext.AddManifest("web.csproj", "<Project/>");
        _fakes.TargetContext.AddFolderNames("src", "src/Web");
    }

    private IRenderedComponent<AdaptSkillDialog> Render()
    {
        var dialog = Render<AdaptSkillDialog>(parameters => parameters
            .Add(component => component.Source, Api)
            .Add(component => component.SourceAgent, Agent.ClaudeCode)
            .Add(component => component.Skill, Docker)
            .Add(component => component.OnClose, () => _closed++)
            .Add(component => component.OnSaved, (SkillCopyOutcome outcome) => _saved.Add(outcome)));
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".adapt-provider")));
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".provider-picker-trigger, .adapt-provider-line")));

        return dialog;
    }

    private IRenderedComponent<AdaptSkillDialog> RenderWithTarget()
    {
        var dialog = Render();
        ChooseWeb(dialog);

        return dialog;
    }

    private static void ChooseWeb(IRenderedComponent<AdaptSkillDialog> dialog)
    {
        dialog.Find(".place-picker-button").Click();
        dialog.FindAll(".place-option")
            .Single(option => option.QuerySelector(".place-option-name")!.TextContent == "web")
            .Click();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".shared-list li")));
    }

    private static IElement Trigger(IRenderedComponent<AdaptSkillDialog> dialog) => dialog.Find(".provider-picker-trigger");

    private static string TriggerText(IRenderedComponent<AdaptSkillDialog> dialog) => Trigger(dialog).TextContent.Trim();

    private static void PickFromList(IRenderedComponent<AdaptSkillDialog> dialog, string name)
    {
        Trigger(dialog).Click();
        dialog.FindAll("[role=option]")
            .Single(option => option.QuerySelector(".provider-option-name")!.TextContent == name)
            .Click();
    }

    private static IElement Button(IRenderedComponent<AdaptSkillDialog> dialog, string text) =>
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private static string[] SharedItems(IRenderedComponent<AdaptSkillDialog> dialog) =>
        dialog.FindAll(".shared-list li").Select(item => item.TextContent.Trim()).ToArray();

    private static string Note(IRenderedComponent<AdaptSkillDialog> dialog) => dialog.Find(".adapt-provider-note").TextContent.Trim();

    private void Adapt(IRenderedComponent<AdaptSkillDialog> dialog)
    {
        Button(dialog, "Adapt").Click();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-adapt-review")));
    }

    private string[] PressedAgent(IRenderedComponent<AdaptSkillDialog> dialog) =>
        [.. dialog.FindAll(".agent-switch button").Where(button => button.GetAttribute("aria-pressed") == "true").Select(button => button.TextContent)];

    // ---- which provider the dialog starts with

    [Fact]
    public void Starts_with_the_first_provider_when_both_are_ready_and_none_was_used_before()
    {
        var dialog = Render();

        Assert.Equal("Claude▾", TriggerText(dialog));
        Assert.Equal("· Connected via Claude Code", dialog.Find(".adapt-provider-line").TextContent.Trim());
    }

    [Fact]
    public void Starts_with_the_first_ready_provider_when_none_was_used_before()
    {
        _fakes.ProviderStatus.State = AiProviderState.SignedOut;

        var dialog = Render();

        Assert.Equal("Codex▾", TriggerText(dialog));
    }

    [Fact]
    public void Starts_with_the_first_provider_of_the_catalog_when_none_is_ready_and_none_was_used_before()
    {
        _fakes.ProviderStatus.State = AiProviderState.NotInstalled;
        _fakes.CodexStatus.State = AiProviderState.SignedOut;

        var dialog = Render();

        Assert.Equal("Claude▾", TriggerText(dialog));
        Assert.Equal("Claude Code isn't installed.", dialog.Find(".adapt-provider-line").TextContent.Trim());
    }

    [Fact]
    public void Starts_with_the_provider_used_last_time_even_when_it_is_not_ready_and_another_one_is()
    {
        _fakes.LastUsed.Remembered = UiFakes.CodexProvider.Id;
        _fakes.CodexStatus.State = AiProviderState.SignedOut;

        var dialog = Render();

        Assert.Equal("Codex▾", TriggerText(dialog));
        Assert.Equal("Sign in with ChatGPT", dialog.Find(".provider-action").TextContent.Trim());
        Assert.Equal("Sign in to Codex CLI to continue.", dialog.Find(".adapt-provider-line").TextContent.Trim());
    }

    [Fact]
    public void Offers_no_picker_when_the_catalog_has_one_provider()
    {
        _fakes.AddProviderTo(Services, UiFakes.Provider, _fakes.ProviderStatus, _fakes.SkillAdapter);

        var dialog = Render();

        Assert.Empty(dialog.FindAll(".provider-picker"));
        Assert.Equal("Claude · Connected via Claude Code", dialog.Find(".adapt-provider-line").TextContent.Trim());
    }

    // ---- the picker

    [Fact]
    public void The_list_names_every_provider_and_how_it_is_doing()
    {
        _fakes.CodexStatus.State = AiProviderState.NotInstalled;
        var dialog = Render();

        Trigger(dialog).Click();

        Assert.Equal(
            ["Claude|Connected via Claude Code", "Codex|Not installed"],
            dialog.FindAll("[role=option]").Select(option =>
                $"{option.QuerySelector(".provider-option-name")!.TextContent}|{option.QuerySelector(".provider-option-state")!.TextContent}"));
    }

    [Fact]
    public void The_arrow_keys_and_enter_choose_a_provider_without_the_mouse()
    {
        var dialog = Render();

        Trigger(dialog).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        dialog.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        dialog.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Codex▾", TriggerText(dialog));
    }

    [Fact]
    public void Escape_in_the_open_list_closes_the_list_and_not_the_dialog()
    {
        var dialog = Render();
        Trigger(dialog).Click();

        dialog.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(dialog.FindAll("[role=listbox]"));
        Assert.Equal(0, _closed);
        Assert.NotEmpty(dialog.FindAll("[role=dialog]"));

        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(1, _closed);
    }

    [Fact]
    public void Choosing_a_provider_shows_the_state_of_that_provider_and_its_own_note()
    {
        var dialog = Render();

        PickFromList(dialog, "Codex");

        Assert.Equal("Codex▾", TriggerText(dialog));
        Assert.Equal("· Connected via Codex CLI", dialog.Find(".adapt-provider-line").TextContent.Trim());
        Assert.Equal("Counts toward your ChatGPT plan unless an API key is set for Codex.", Note(dialog));
    }

    [Theory]
    [InlineData(AiUsageBasis.Subscription)]
    [InlineData(AiUsageBasis.ApiKey)]
    [InlineData(AiUsageBasis.Unknown)]
    public void The_note_of_codex_is_the_same_whatever_its_client_reports(AiUsageBasis basis)
    {
        _fakes.CodexStatus.UsageBasis = basis;
        var dialog = Render();

        PickFromList(dialog, "Codex");

        Assert.Equal("Counts toward your ChatGPT plan unless an API key is set for Codex.", Note(dialog));
    }

    [Fact]
    public void Going_back_to_claude_reads_how_claude_is_signed_in_again()
    {
        _fakes.ProviderStatus.UsageBasis = AiUsageBasis.ApiKey;
        var dialog = Render();
        dialog.WaitForAssertion(() => Assert.Equal("Uses your Anthropic API key.", Note(dialog)));

        PickFromList(dialog, "Codex");
        PickFromList(dialog, "Claude");

        dialog.WaitForAssertion(() => Assert.Equal("Uses your Anthropic API key.", Note(dialog)));
    }

    [Fact]
    public void A_provider_that_is_not_installed_shows_its_install_help_in_its_own_state()
    {
        _fakes.CodexStatus.State = AiProviderState.NotInstalled;
        var dialog = Render();

        PickFromList(dialog, "Codex");
        dialog.Find(".provider-action").Click();

        Assert.Equal("How to install", dialog.Find(".provider-action").TextContent.Trim());
        Assert.Equal([UiFakes.CodexProvider.InstallHelp], _fakes.Links.Opened);
        Assert.True(dialog.Find(".dialog-footer .button-primary").HasAttribute("disabled"));
    }

    // ---- signing in with the chosen provider

    [Fact]
    public void Signing_in_asks_the_chosen_provider_and_not_the_other_one()
    {
        _fakes.CodexStatus.State = AiProviderState.SignedOut;
        var dialog = Render();
        PickFromList(dialog, "Codex");

        dialog.Find(".provider-action").Click();

        dialog.WaitForAssertion(() => Assert.Equal(1, _fakes.CodexStatus.SignInsStarted));
        Assert.Equal(0, _fakes.ProviderStatus.SignInsStarted);
        _fakes.CodexStatus.FinishSignIn();
        dialog.WaitForAssertion(() => Assert.Equal("· Connected via Codex CLI", dialog.Find(".adapt-provider-line").TextContent.Trim()));
    }

    [Fact]
    public void The_picker_is_closed_while_a_sign_in_is_awaited()
    {
        _fakes.CodexStatus.State = AiProviderState.SignedOut;
        var dialog = Render();
        PickFromList(dialog, "Codex");

        dialog.Find(".provider-action").Click();

        dialog.WaitForAssertion(() => Assert.True(Trigger(dialog).HasAttribute("disabled")));
        Assert.Equal("Finish signing in in your browser…", dialog.Find(".adapt-provider-line").TextContent.Trim());
        _fakes.CodexStatus.FinishSignIn();
        dialog.WaitForAssertion(() => Assert.False(Trigger(dialog).HasAttribute("disabled")));
    }

    // ---- the provider never changes the target

    [Fact]
    public void Choosing_a_provider_leaves_the_target_agent_the_target_place_and_the_shared_list_as_they_are()
    {
        var dialog = RenderWithTarget();
        var shared = SharedItems(dialog);
        var place = dialog.Find(".place-picker-button").TextContent;

        PickFromList(dialog, "Codex");

        Assert.Equal(["Claude"], PressedAgent(dialog));
        Assert.Contains("web", place);
        Assert.Equal(place, dialog.Find(".place-picker-button").TextContent);
        Assert.Equal(shared, SharedItems(dialog));
    }

    [Fact]
    public void Adapting_with_codex_for_a_claude_target_hands_the_request_to_codex_and_saves_to_the_claude_target()
    {
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");

        Adapt(dialog);
        Button(dialog, "Save to web").Click();

        dialog.WaitForAssertion(() => Assert.Equal(1, _closed));
        Assert.Single(_fakes.CodexAdapter.Prompts);
        Assert.Empty(_fakes.SkillAdapter.Prompts);
        Assert.Equal(Agent.ClaudeCode, Assert.Single(_fakes.AdaptedSkillWriter.Targets).Agent);
    }

    [Fact]
    public void Choosing_the_target_agent_does_not_change_the_provider()
    {
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");

        dialog.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();

        dialog.WaitForAssertion(() => Assert.Equal(["Codex"], PressedAgent(dialog)));
        Assert.Equal("Codex▾", TriggerText(dialog));
    }

    // ---- what the provider's client loads by itself

    [Fact]
    public void A_file_the_client_loads_by_itself_is_listed_last_in_the_summary_and_in_show_all()
    {
        _fakes.CodexOwnFiles.Files.Add(new ProviderLoadedFile(".codex/AGENTS.md", 1200));
        var dialog = RenderWithTarget();

        PickFromList(dialog, "Codex");

        dialog.WaitForAssertion(() => Assert.Equal(
            [
                "SKILL.md from docker · 1 KB",
                "2 project files · 3 KB",
                "Folder names in web (two levels)",
                "~/.codex/AGENTS.md (loaded by Codex) · 2 KB",
            ],
            SharedItems(dialog)));

        dialog.Find(".shared-toggle").Click();

        Assert.Equal("~/.codex/AGENTS.md (loaded by Codex) · 2 KB", SharedItems(dialog).Last());
        Assert.Equal(5, SharedItems(dialog).Length);
    }

    [Fact]
    public void The_file_the_client_loads_itself_is_listed_before_a_target_is_chosen_too_and_goes_with_its_provider()
    {
        _fakes.CodexOwnFiles.Files.Add(new ProviderLoadedFile(".codex/AGENTS.md", 1200));
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");
        dialog.WaitForAssertion(() => Assert.Equal(4, SharedItems(dialog).Length));

        PickFromList(dialog, "Claude");

        dialog.WaitForAssertion(() => Assert.Equal(3, SharedItems(dialog).Length));
        Assert.DoesNotContain(SharedItems(dialog), line => line.Contains("loaded by"));
    }

    [Fact]
    public void The_file_the_client_loads_itself_is_shown_but_never_sent_nor_counted()
    {
        _fakes.CodexOwnFiles.Files.Add(new ProviderLoadedFile(".codex/AGENTS.md", 1200));
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");
        dialog.WaitForAssertion(() => Assert.Equal(4, SharedItems(dialog).Length));
        dialog.Find(".shared-toggle").Click();
        var listed = SharedItems(dialog).Select(item => item.Split(" · ")[0]).Where(item => !item.Contains("loaded by")).ToArray();

        Adapt(dialog);

        var prompt = Assert.Single(_fakes.CodexAdapter.Prompts);
        var titles = prompt.Content.Split('\n').Where(line => line.StartsWith("<<< ") && line.EndsWith(" >>>")).Select(line => line[4..^4]);
        Assert.Equal(listed, titles);
        Assert.DoesNotContain("AGENTS.md", prompt.Content);
        Assert.DoesNotContain("AGENTS.md", prompt.Instructions);
        Assert.DoesNotContain("loaded by", prompt.Content);
    }

    // ---- remembering the provider

    [Fact]
    public void Remembers_the_provider_when_adapt_is_pressed_and_not_when_it_is_only_chosen()
    {
        var dialog = RenderWithTarget();

        PickFromList(dialog, "Codex");

        Assert.Equal(0, _fakes.LastUsed.Writes);

        Adapt(dialog);

        Assert.Equal(UiFakes.CodexProvider.Id, _fakes.LastUsed.Remembered);
    }

    [Fact]
    public void Remembers_the_provider_even_when_the_adaptation_fails()
    {
        _fakes.CodexAdapter.FailsWith(new AdaptationFailure.TimedOut());
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");

        Button(dialog, "Adapt").Click();
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".dialog-footer-message")));

        Assert.Equal(UiFakes.CodexProvider.Id, _fakes.LastUsed.Remembered);
        Assert.Equal("Codex CLI took too long, so Scion stopped it. Try again.", dialog.Find(".dialog-footer-message").TextContent.Trim());
    }

    [Fact]
    public void A_provider_that_turns_out_signed_out_is_marked_so_in_the_list_and_the_other_one_is_left_alone()
    {
        _fakes.CodexAdapter.FailsWith(new AdaptationFailure.SignedOut());
        var dialog = RenderWithTarget();
        PickFromList(dialog, "Codex");

        Button(dialog, "Adapt").Click();

        dialog.WaitForAssertion(() => Assert.Equal("Sign in with ChatGPT", dialog.Find(".provider-action").TextContent.Trim()));
        Trigger(dialog).Click();
        Assert.Equal(
            ["Connected via Claude Code", "Sign in needed"],
            dialog.FindAll(".provider-option-state").Select(state => state.TextContent));
    }
}
