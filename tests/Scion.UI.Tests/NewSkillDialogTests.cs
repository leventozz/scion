using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Creating;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class NewSkillDialogTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly UiFakes _fakes = new();
    private readonly List<SkillSummary> _created = [];
    private int _closed;

    public NewSkillDialogTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
    }

    [Fact]
    public void Is_a_modal_dialog_named_New_skill_that_says_where_the_skill_will_go()
    {
        var dialog = Render();

        Assert.Equal("true", dialog.Find("[role=dialog]").GetAttribute("aria-modal"));
        Assert.Equal("New skill", dialog.Find("h2").TextContent);
        Assert.Equal("In api · Claude", dialog.Find(".dialog-subtitle").TextContent);
        Assert.NotNull(dialog.Find("button[aria-label=Close]"));
    }

    [Fact]
    public void Names_personal_and_the_other_agent_in_the_subtitle()
    {
        var dialog = Render(new Location.Personal(), "Personal", Agent.Codex);

        Assert.Equal("In Personal · Codex", dialog.Find(".dialog-subtitle").TextContent);
    }

    [Fact]
    public void Puts_the_focus_on_the_name()
    {
        var dialog = Render();

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(NameInput(dialog));
    }

    [Fact]
    public void Asks_the_browser_to_ignore_Enter_in_the_description()
    {
        var dialog = Render();

        var call = JSInterop.VerifyInvoke("scion.ignoreEnter");
        call.Arguments[0].ShouldBeElementReferenceTo(DescriptionInput(dialog));
    }

    [Fact]
    public void Starts_with_both_hints_no_problem_and_Create_off()
    {
        var dialog = Render();

        Assert.Equal(
            ["Lowercase letters, numbers and hyphens.", "What the skill does and when to use it."],
            dialog.FindAll(".field-hint").Select(hint => hint.TextContent));
        Assert.Empty(dialog.FindAll(".field-problem"));
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
        Assert.Equal(["Cancel", "Create"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void Create_is_the_one_primary_button()
    {
        var dialog = Render();

        Assert.Equal(["Create"], dialog.FindAll(".button-primary").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void Turns_on_Create_once_name_and_description_are_valid()
    {
        var dialog = Render();

        Type(dialog, "code-review", "Reviews pull requests.");

        Assert.False(CreateButton(dialog).HasAttribute("disabled"));
        Assert.Empty(dialog.FindAll(".field-problem"));
    }

    [Fact]
    public void A_name_that_breaks_the_rules_shows_an_amber_line_instead_of_the_hint_and_keeps_Create_off()
    {
        var dialog = Render();

        Type(dialog, "Release_Notes", "Reviews pull requests.");

        var problem = dialog.Find(".field-problem");
        Assert.Equal("Use lowercase letters, numbers and hyphens.", problem.TextContent);
        Assert.NotNull(problem.QuerySelector(".warning-icon"));
        Assert.Equal("true", NameInput(dialog).GetAttribute("aria-invalid"));
        Assert.DoesNotContain(dialog.FindAll(".field-hint"), hint => hint.TextContent.StartsWith("Lowercase"));
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void An_empty_name_is_not_reported_when_the_user_clears_the_field()
    {
        var dialog = Render();
        Type(dialog, "Bad", "Reviews pull requests.");

        NameInput(dialog).Input("");

        Assert.Empty(dialog.FindAll(".field-problem"));
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_name_that_is_already_a_skill_here_says_so_and_keeps_Create_off()
    {
        var dialog = Render();

        Type(dialog, "docker", "Runs containers.");

        Assert.Equal("A skill with this name already exists here.", dialog.Find(".field-problem").TextContent);
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_description_over_the_limit_shows_an_amber_line_under_the_description()
    {
        var dialog = Render();

        Type(dialog, "code-review", new string('a', 1025));

        Assert.Equal("Use at most 1024 characters.", dialog.Find(".field-problem").TextContent);
        Assert.Equal("true", DescriptionInput(dialog).GetAttribute("aria-invalid"));
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void An_empty_description_is_not_reported_but_keeps_Create_off()
    {
        var dialog = Render();

        Type(dialog, "code-review", "   ");

        Assert.Empty(dialog.FindAll(".field-problem"));
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Line_breaks_in_the_description_become_spaces()
    {
        var dialog = Render();

        DescriptionInput(dialog).Input("Reviews\npull requests.");

        Assert.Equal("Reviews pull requests.", DescriptionInput(dialog).GetAttribute("value"));
    }

    [Fact]
    public void What_is_typed_in_the_description_is_not_written_back_into_the_field()
    {
        var dialog = Render();

        DescriptionInput(dialog).Input("Reviews");

        Assert.Equal("", DescriptionInput(dialog).GetAttribute("value"));
        Assert.Equal("Reviews", ReadDescription(dialog));
    }

    [Fact]
    public void Creates_the_skill_and_hands_it_over_to_the_page_that_opens_it()
    {
        var dialog = Render();
        Type(dialog, "code-review", "  Reviews pull requests.  ");

        CreateButton(dialog).Click();

        dialog.WaitForAssertion(() => Assert.Single(_created));
        var skill = _created.Single();
        Assert.Equal("code-review", skill.Name);
        Assert.Equal("code-review", skill.FolderName);
        Assert.Equal("Reviews pull requests.", skill.Description);
        Assert.Equal(["code-review"], _fakes.SkillCreation.Created);
    }

    [Fact]
    public void While_it_creates_Create_is_off_so_a_second_click_creates_nothing_more()
    {
        var gate = _fakes.SkillCreation.HoldCreation();
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");

        CreateButton(dialog).Click();

        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
        gate.SetResult();
        dialog.WaitForAssertion(() => Assert.Single(_created));
        Assert.Single(_fakes.SkillCreation.Created);
    }

    [Fact]
    public void A_name_that_was_taken_in_the_meantime_stays_in_the_dialog_says_so_and_is_left_alone()
    {
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");
        _fakes.Reader.Put(Project, Agent.ClaudeCode, new SkillFolder("code-review", "code-review", "Someone else's."));

        CreateButton(dialog).Click();

        dialog.WaitForAssertion(() => Assert.Equal("A skill with this name already exists here.", dialog.Find(".field-problem").TextContent));
        Assert.Empty(_created);
        Assert.Empty(_fakes.SkillCreation.Created);
        Assert.True(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_name_taken_in_the_race_is_free_to_change()
    {
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");
        _fakes.SkillCreation.FailWith(SkillCreateFailure.AlreadyExists);
        CreateButton(dialog).Click();
        dialog.WaitForAssertion(() => Assert.Single(dialog.FindAll(".field-problem")));
        _fakes.SkillCreation.FailWith(null);

        NameInput(dialog).Input("code-reviewer");

        Assert.Empty(dialog.FindAll(".field-problem"));
        Assert.False(CreateButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_failed_write_says_so_next_to_the_buttons_and_lets_the_user_try_again()
    {
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");
        _fakes.SkillCreation.FailWith(SkillCreateFailure.AccessDenied);

        CreateButton(dialog).Click();

        dialog.WaitForAssertion(() => Assert.Equal("The skill couldn't be created.", dialog.Find(".dialog-footer .field-problem").TextContent));
        Assert.Empty(_created);
        Assert.False(CreateButton(dialog).HasAttribute("disabled"));

        _fakes.SkillCreation.FailWith(null);
        CreateButton(dialog).Click();
        dialog.WaitForAssertion(() => Assert.Single(_created));
    }

    [Fact]
    public void Editing_a_field_clears_the_failed_write_message()
    {
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");
        _fakes.SkillCreation.FailWith(SkillCreateFailure.Other);
        CreateButton(dialog).Click();
        dialog.WaitForAssertion(() => Assert.Single(dialog.FindAll(".field-problem")));

        DescriptionInput(dialog).Input("Reviews pull requests carefully.");

        Assert.Empty(dialog.FindAll(".field-problem"));
    }

    [Fact]
    public void The_close_button_Cancel_and_Esc_close_it_without_creating_anything()
    {
        var dialog = Render();
        Type(dialog, "code-review", "Reviews pull requests.");

        dialog.Find("button[aria-label=Close]").Click();
        dialog.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == "Cancel").Click();
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(3, _closed);
        Assert.Empty(_fakes.SkillCreation.Created);
    }

    [Fact]
    public void A_click_on_the_backdrop_closes_nothing()
    {
        var dialog = Render();

        // The backdrop listens to nothing, so a click on it cannot close the dialog.
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());

        Assert.Equal(0, _closed);
    }

    private IRenderedComponent<NewSkillDialog> Render(Location? location = null, string placeName = "api", Agent agent = Agent.ClaudeCode) =>
        Render<NewSkillDialog>(parameters => parameters
            .Add(dialog => dialog.Location, location ?? Project)
            .Add(dialog => dialog.PlaceName, placeName)
            .Add(dialog => dialog.Agent, agent)
            .Add(dialog => dialog.OnClose, () => _closed++)
            .Add(dialog => dialog.OnCreated, (SkillSummary skill) => _created.Add(skill)));

    // What the dialog holds is what it creates the skill with, not what it renders into the field.
    private string ReadDescription(IRenderedComponent<NewSkillDialog> dialog)
    {
        NameInput(dialog).Input("code-review");
        CreateButton(dialog).Click();
        dialog.WaitForAssertion(() => Assert.Single(_created));

        return _created.Single().Description;
    }

    private static IElement NameInput(IRenderedComponent<NewSkillDialog> dialog) => dialog.Find("input#new-skill-name");

    private static IElement DescriptionInput(IRenderedComponent<NewSkillDialog> dialog) => dialog.Find("textarea#new-skill-description");

    private static IElement CreateButton(IRenderedComponent<NewSkillDialog> dialog) =>
        dialog.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == "Create");

    private static void Type(IRenderedComponent<NewSkillDialog> dialog, string name, string description)
    {
        NameInput(dialog).Input(name);
        DescriptionInput(dialog).Input(description);
    }
}
