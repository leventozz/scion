using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class LocationScreenCreatingTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly UiFakes _fakes = new();
    private readonly List<SkillSummary> _opened = [];

    public LocationScreenCreatingTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void A_location_with_skills_has_Copy_from_and_the_primary_New_skill_in_the_header()
    {
        AddASkill();

        var screen = RenderProject();

        var buttons = screen.FindAll(".screen-header .button");
        Assert.Equal(["Copy from…", "New skill"], buttons.Select(button => button.TextContent.Trim()));
        Assert.Contains("button-secondary", buttons[0].ClassName);
        Assert.Contains("button-primary", buttons[1].ClassName);
        Assert.NotNull(buttons[1].QuerySelector(".add-icon"));
        Assert.Single(screen.FindAll(".button-primary"));
    }

    [Fact]
    public void An_empty_location_keeps_the_header_empty_and_has_Copy_from_primary_and_New_skill_secondary_in_the_empty_state()
    {
        var screen = RenderProject();

        Assert.Empty(screen.FindAll(".screen-header .button"));
        var buttons = screen.FindAll(".empty-state .button");
        Assert.Equal(["Copy from…", "New skill"], buttons.Select(button => button.TextContent.Trim()));
        Assert.Contains("button-primary", buttons[0].ClassName);
        Assert.Contains("button-secondary", buttons[1].ClassName);
        Assert.Single(screen.FindAll(".button-primary"));
    }

    [Fact]
    public void New_skill_from_the_header_opens_the_dialog_for_this_place_and_agent()
    {
        AddASkill();
        var screen = RenderProject();

        HeaderNewSkill(screen).Click();

        Assert.Equal("In api · Claude", screen.Find(".dialog-subtitle").TextContent);
    }

    [Fact]
    public void New_skill_from_the_empty_state_opens_the_same_dialog_in_personal_for_the_selected_agent()
    {
        var screen = RenderPersonal();
        screen.Find(".agent-switch button:last-child").Click();

        screen.FindAll(".empty-state .button").Single(button => button.TextContent.Trim() == "New skill").Click();

        Assert.Equal("In Personal · Codex", screen.Find(".dialog-subtitle").TextContent);
    }

    [Fact]
    public void Creating_a_skill_closes_the_dialog_and_opens_the_new_skill_for_editing()
    {
        var screen = RenderProject();
        screen.FindAll(".empty-state .button").Single(button => button.TextContent.Trim() == "New skill").Click();
        screen.Find("input#new-skill-name").Input("code-review");
        screen.Find("textarea#new-skill-description").Input("Reviews pull requests.");

        screen.Find(".dialog .button-primary").Click();

        screen.WaitForAssertion(() => Assert.Single(_opened));
        Assert.Equal("code-review", _opened.Single().FolderName);
        Assert.Empty(screen.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Cancel_and_Esc_close_the_dialog_and_create_nothing()
    {
        AddASkill();
        var screen = RenderProject();

        HeaderNewSkill(screen).Click();
        screen.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == "Cancel").Click();
        Assert.Empty(screen.FindAll("[role=dialog]"));

        HeaderNewSkill(screen).Click();
        screen.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Empty(_fakes.SkillCreation.Created);
    }

    [Fact]
    public void The_instruction_file_tab_has_no_New_skill()
    {
        AddASkill();
        var screen = RenderProject();

        screen.FindAll("[role=tab]")[1].Click();

        Assert.DoesNotContain(screen.FindAll("button"), button => button.TextContent.Trim() == "New skill");
    }

    [Fact]
    public void A_project_folder_that_is_gone_has_no_New_skill()
    {
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Project("/work/gone"))
            .Add(screen => screen.Title, "gone")
            .Add(screen => screen.FolderMissing, true));

        Assert.DoesNotContain(screen.FindAll("button"), button => button.TextContent.Contains("New skill"));
    }

    private void AddASkill() =>
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));

    private static IElement HeaderNewSkill(IRenderedComponent<LocationScreen> screen) =>
        screen.FindAll(".screen-header .button").Single(button => button.TextContent.Trim() == "New skill");

    private IRenderedComponent<LocationScreen> RenderProject() =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, Project)
            .Add(screen => screen.Title, "api")
            .Add(screen => screen.OnEditSkill, (SkillSummary skill) => _opened.Add(skill)));

    private IRenderedComponent<LocationScreen> RenderPersonal() =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Personal())
            .Add(screen => screen.Title, "Personal"));
}
