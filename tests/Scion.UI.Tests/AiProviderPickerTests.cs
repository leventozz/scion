using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Adapting;

namespace Scion.UI.Tests;

public class AiProviderPickerTests : BunitContext
{
    private static readonly AiProvider Claude = UiFakes.Provider;
    private static readonly AiProvider Codex = UiFakes.CodexProvider;

    private readonly List<AiProvider> _chosen = [];

    public AiProviderPickerTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<AiProviderPicker> Render(
        AiProvider? chosen = null,
        AiProviderState claude = AiProviderState.Ready,
        AiProviderState codex = AiProviderState.SignedOut,
        bool disabled = false) =>
        Render<AiProviderPicker>(parameters => parameters
            .Add(picker => picker.Providers, [Claude, Codex])
            .Add(picker => picker.States, new Dictionary<AiProviderId, AiProviderState> { [Claude.Id] = claude, [Codex.Id] = codex })
            .Add(picker => picker.Chosen, chosen ?? Claude)
            .Add(picker => picker.Disabled, disabled)
            .Add(picker => picker.OnChosen, (AiProvider provider) => _chosen.Add(provider)));

    private static IElement Trigger(IRenderedComponent<AiProviderPicker> picker) => picker.Find(".provider-picker-trigger");

    private static IElement List(IRenderedComponent<AiProviderPicker> picker) => picker.Find("[role=listbox]");

    private static IElement[] Options(IRenderedComponent<AiProviderPicker> picker) => [.. picker.FindAll("[role=option]")];

    private Bunit.JSRuntimeInvocation LastFocus() => Focuses().Last();

    private Bunit.JSRuntimeInvocation[] Focuses() =>
        [.. JSInterop.Invocations.Where(invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus")];

    // Element references are new on every render in bUnit, so the trigger cannot be told by its reference: what is checked is
    // that the list had the focus (first) and that after closing it something else got it.
    private void AssertFocusWentBackToTheTrigger(int focusesBefore)
    {
        var focuses = Focuses();
        Assert.Equal(focusesBefore + 1, focuses.Length);
        Assert.NotEqual(((Microsoft.AspNetCore.Components.ElementReference)focuses[0].Arguments[0]!).Id,
            ((Microsoft.AspNetCore.Components.ElementReference)focuses[^1].Arguments[0]!).Id);
    }

    private static string? ActiveOption(IRenderedComponent<AiProviderPicker> picker) =>
        picker.Find("[role=option].active").QuerySelector(".provider-option-name")!.TextContent;

    [Fact]
    public void The_trigger_names_the_chosen_provider_and_says_it_opens_a_list()
    {
        var picker = Render(Codex);

        Assert.Equal("Codex▾", Trigger(picker).TextContent.Trim());
        Assert.Equal("listbox", Trigger(picker).GetAttribute("aria-haspopup"));
        Assert.Equal("false", Trigger(picker).GetAttribute("aria-expanded"));
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Clicking_the_trigger_opens_a_list_with_every_provider_and_how_it_is_doing()
    {
        var picker = Render();

        Trigger(picker).Click();

        Assert.Equal("true", Trigger(picker).GetAttribute("aria-expanded"));
        Assert.Equal(
            ["Claude|Connected via Claude Code", "Codex|Sign in needed"],
            Options(picker).Select(option =>
                $"{option.QuerySelector(".provider-option-name")!.TextContent}|{option.QuerySelector(".provider-option-state")!.TextContent}"));
    }

    [Fact]
    public void A_provider_that_is_not_installed_says_so_in_its_row()
    {
        var picker = Render(codex: AiProviderState.NotInstalled);

        Trigger(picker).Click();

        Assert.Equal("Not installed", Options(picker)[1].QuerySelector(".provider-option-state")!.TextContent);
    }

    [Fact]
    public void The_chosen_provider_is_selected_in_the_list_and_carries_a_check_mark_that_is_drawn_not_a_font()
    {
        var picker = Render(Codex);

        Trigger(picker).Click();

        var options = Options(picker);
        Assert.Equal(["false", "true"], options.Select(option => option.GetAttribute("aria-selected")));
        Assert.Empty(options[0].QuerySelectorAll(".provider-option-check"));
        Assert.NotNull(options[1].QuerySelector("svg.provider-option-check"));
    }

    [Fact]
    public void Choosing_a_row_tells_the_caller_and_closes_the_list()
    {
        var picker = Render();
        Trigger(picker).Click();

        Options(picker)[1].Click();

        Assert.Equal([Codex], _chosen);
        Assert.Empty(picker.FindAll("[role=listbox]"));
        Assert.Equal("false", Trigger(picker).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Choosing_the_provider_that_is_already_chosen_only_closes_the_list()
    {
        var picker = Render();
        Trigger(picker).Click();

        Options(picker)[0].Click();

        Assert.Empty(_chosen);
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Clicking_outside_the_list_closes_it_without_choosing()
    {
        var picker = Render();
        Trigger(picker).Click();

        picker.Find(".provider-picker-scrim").Click();

        Assert.Empty(picker.FindAll("[role=listbox]"));
        Assert.Empty(_chosen);
    }

    [Fact]
    public void The_arrow_down_key_on_the_trigger_opens_the_list()
    {
        var picker = Render();

        Trigger(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal("true", Trigger(picker).GetAttribute("aria-expanded"));
        Assert.Equal(2, Options(picker).Length);
    }

    [Fact]
    public void Opening_puts_the_focus_in_the_list_and_the_chosen_provider_is_the_active_one()
    {
        var picker = Render(Codex);

        Trigger(picker).Click();

        Assert.Equal("Codex", ActiveOption(picker));
        LastFocus().Arguments[0].ShouldBeElementReferenceTo(List(picker));
    }

    [Fact]
    public void The_arrow_keys_move_the_active_provider_and_stop_at_the_ends()
    {
        var picker = Render();
        Trigger(picker).Click();

        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("Claude", ActiveOption(picker));

        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("Codex", ActiveOption(picker));

        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("Codex", ActiveOption(picker));
        Assert.Equal(Options(picker)[1].Id, List(picker).GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void Enter_chooses_the_active_provider()
    {
        var picker = Render();
        Trigger(picker).Click();
        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        List(picker).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([Codex], _chosen);
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Space_chooses_the_active_provider()
    {
        var picker = Render();
        Trigger(picker).Click();
        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        List(picker).KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Equal([Codex], _chosen);
    }

    [Fact]
    public void Escape_closes_the_list_without_choosing_and_the_focus_goes_back_to_the_trigger()
    {
        var picker = Render();
        Trigger(picker).Click();
        List(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        var focusesBefore = Focuses().Length;

        List(picker).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(picker.FindAll("[role=listbox]"));
        Assert.Empty(_chosen);
        AssertFocusWentBackToTheTrigger(focusesBefore);
    }

    [Fact]
    public void Choosing_returns_the_focus_to_the_trigger()
    {
        var picker = Render();
        Trigger(picker).Click();
        var focusesBefore = Focuses().Length;

        Options(picker)[1].Click();

        AssertFocusWentBackToTheTrigger(focusesBefore);
    }

    [Fact]
    public void A_disabled_picker_cannot_be_opened()
    {
        var picker = Render(disabled: true);

        Assert.True(Trigger(picker).HasAttribute("disabled"));
        Trigger(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }
}
