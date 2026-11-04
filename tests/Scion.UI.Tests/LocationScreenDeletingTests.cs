using AngleSharp.Dom;
using Bunit;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class LocationScreenDeletingTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly UiFakes _fakes = new();

    public LocationScreenDeletingTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Delete_comes_last_in_the_row_menu_after_a_separator()
    {
        AddSkills(new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject();

        OpenMenu(screen, 0);

        var menu = screen.Find(".skill-row .options-menu");
        Assert.Equal(
            ["menuitem", "menuitem", "menuitem", "separator", "menuitem"],
            menu.Children.Select(child => child.GetAttribute("role")));
        Assert.Equal("Delete", menu.Children[^1].TextContent.Trim());
    }

    [Fact]
    public void A_linked_skill_has_neither_Delete_nor_the_separator()
    {
        AddSkills(new SkillFolder("shared", "shared", "Lives elsewhere.", IsLinked: true));
        var screen = RenderProject();

        OpenMenu(screen, 0);

        Assert.Equal(
            ["Edit", "Copy to…", "Adapt with AI…"],
            screen.FindAll(".skill-row [role=menuitem]").Select(item => item.TextContent.Trim()));
        Assert.Empty(screen.FindAll(".skill-row [role=separator]"));
    }

    [Fact]
    public void Delete_closes_the_menu_and_asks_first()
    {
        AddSkills(new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject();
        OpenMenu(screen, 0);

        ChooseDelete(screen);

        Assert.Empty(screen.FindAll(".skill-row [role=menu]"));
        Assert.Equal("Delete docker?", screen.Find("[role=dialog] h2").TextContent);
        Assert.Equal("api · Claude", screen.Find(".dialog-context").TextContent);
        Assert.Empty(_fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void Asks_about_the_skill_of_the_row_that_was_chosen()
    {
        AddSkills(new SkillFolder("alpha", "alpha", "First."), new SkillFolder("beta", "beta", "Second."));
        var screen = RenderProject();
        OpenMenu(screen, 1);

        ChooseDelete(screen);

        Assert.Equal("beta", screen.Find("[role=dialog] h2 em").TextContent);
    }

    [Fact]
    public void Names_personal_in_the_dialog_of_a_personal_skill()
    {
        _fakes.Reader.AddPersonal(Agent.Codex, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(location => location.Location, new Location.Personal())
            .Add(location => location.Title, "Personal")
            .Add(location => location.Agent, Agent.Codex));
        OpenMenu(screen, 0);

        ChooseDelete(screen);

        Assert.Equal("Personal · Codex", screen.Find(".dialog-context").TextContent);
    }

    [Fact]
    public void Cancel_keeps_the_skill()
    {
        AddSkills(new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject();
        OpenMenu(screen, 0);
        ChooseDelete(screen);

        Button(screen, "Cancel").Click();

        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Equal(["docker"], screen.FindAll(".skill-row .skill-name").Select(name => name.TextContent));
        Assert.Empty(_fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void Deleting_closes_the_dialog_and_the_skill_leaves_the_list()
    {
        AddSkills(new SkillFolder("alpha", "alpha", "First."), new SkillFolder("beta", "beta", "Second."));
        var screen = RenderProject();
        OpenMenu(screen, 0);
        ChooseDelete(screen);

        Button(screen, "Delete").Click();

        screen.WaitForAssertion(() => Assert.Empty(screen.FindAll("[role=dialog]")));
        Assert.Equal(["beta"], screen.FindAll(".skill-row .skill-name").Select(name => name.TextContent));
        Assert.Equal(["alpha"], _fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void Deleting_the_last_skill_shows_the_empty_state()
    {
        AddSkills(new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject();
        OpenMenu(screen, 0);
        ChooseDelete(screen);

        Button(screen, "Delete").Click();

        screen.WaitForAssertion(() => Assert.Empty(screen.FindAll(".skill-row")));
        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Contains(screen.FindAll("button"), button => button.TextContent.Contains("New skill"));
    }

    [Fact]
    public void A_half_removed_original_keeps_the_dialog_until_it_is_closed_and_then_refreshes_the_list()
    {
        AddSkills(new SkillFolder("docker", "docker", "Runs compose files."), new SkillFolder("testing", "testing", "Runs tests."));
        _fakes.SkillTrash.LeaveOriginalBehind();
        var screen = RenderProject();
        OpenMenu(screen, 0);
        ChooseDelete(screen);
        Button(screen, "Delete").Click();
        screen.WaitForAssertion(() => Assert.Single(screen.FindAll(".dialog-footer .field-problem")));

        Button(screen, "Close").Click();

        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Equal(["testing"], screen.FindAll(".skill-row .skill-name").Select(name => name.TextContent));
    }

    private void AddSkills(params SkillFolder[] skills) => _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, skills);

    private static void OpenMenu(IRenderedComponent<LocationScreen> screen, int row) =>
        screen.FindAll(".skill-row .options-button")[row].Click();

    private static void ChooseDelete(IRenderedComponent<LocationScreen> screen) =>
        screen.FindAll(".skill-row [role=menuitem]").Single(item => item.TextContent.Trim() == "Delete").Click();

    private static IElement Button(IRenderedComponent<LocationScreen> screen, string text) =>
        screen.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == text);

    private IRenderedComponent<LocationScreen> RenderProject() =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, Project)
            .Add(screen => screen.Title, "api"));
}
