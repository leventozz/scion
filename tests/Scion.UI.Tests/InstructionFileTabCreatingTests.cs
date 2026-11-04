using AngleSharp.Dom;
using Bunit;
using Scion.Application.Creating;
using Scion.Domain;

namespace Scion.UI.Tests;

public class InstructionFileTabCreatingTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly Location Personal = new Location.Personal();

    private readonly UiFakes _fakes = new();

    public InstructionFileTabCreatingTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(Agent.ClaudeCode, "Create CLAUDE.md")]
    [InlineData(Agent.Codex, "Create AGENTS.md")]
    public void The_empty_state_offers_one_primary_button_named_after_the_file(Agent agent, string expected)
    {
        var screen = OpenInstructionTab(Render(Project, "api", agent));

        Assert.Equal(expected, screen.Find(".instruction-empty-state .button-primary").TextContent.Trim());
        Assert.Single(screen.FindAll(".button-primary"));
        Assert.Equal("No " + expected["Create ".Length..] + " in this project", screen.Find(".instruction-empty").TextContent);
    }

    [Fact]
    public void A_file_that_exists_has_no_Create_button()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Rules\n"));

        var screen = OpenInstructionTab(Render(Project, "api"));

        Assert.DoesNotContain(screen.FindAll("button"), button => button.TextContent.Contains("Create"));
    }

    [Fact]
    public void Creating_a_project_file_opens_it_for_editing_with_the_folder_name_as_its_title()
    {
        var screen = OpenInstructionTab(Render(Project, "api"));

        screen.Find(".instruction-empty-state .button-primary").Click();

        screen.WaitForAssertion(() => Assert.Equal("# api\n\n", screen.Find("textarea").GetAttribute("value")));
        Assert.Equal("./CLAUDE.md", screen.Find(".instruction-file-path").TextContent);
        Assert.Equal(["Cancel", "Save"], screen.FindAll(".instruction-file-bar .button").Select(button => button.TextContent.Trim()));
        Assert.Empty(screen.FindAll(".instruction-empty-state"));
    }

    [Fact]
    public void Creating_a_personal_file_opens_an_empty_editor_for_the_file_of_the_selected_agent()
    {
        var screen = OpenInstructionTab(Render(Personal, "Personal", Agent.Codex));

        screen.Find(".instruction-empty-state .button-primary").Click();

        screen.WaitForAssertion(() => Assert.Equal("", screen.Find("textarea").GetAttribute("value")));
        Assert.Equal("~/.codex/AGENTS.md", screen.Find(".instruction-file-path").TextContent);
    }

    [Fact]
    public void Cancel_after_creating_leaves_the_empty_file_in_view_mode_and_keeps_it()
    {
        var screen = OpenInstructionTab(Render(Project, "api"));
        screen.Find(".instruction-empty-state .button-primary").Click();
        screen.WaitForAssertion(() => Assert.NotNull(screen.Find("textarea")));

        screen.FindAll(".instruction-file-bar .button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        screen.WaitForAssertion(() => Assert.Empty(screen.FindAll("textarea")));
        Assert.Equal("./CLAUDE.md", screen.Find(".instruction-file-path").TextContent);
        Assert.NotNull(_fakes.InstructionFiles.Get(Project, Agent.ClaudeCode, "CLAUDE.md"));
    }

    [Fact]
    public void A_file_that_appeared_in_the_meantime_is_shown_untouched_with_a_calm_note()
    {
        var screen = OpenInstructionTab(Render(Project, "api"));
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Mine\n"));

        screen.Find(".instruction-empty-state .button-primary").Click();

        screen.WaitForAssertion(() => Assert.Equal("Mine", screen.Find(".instruction-file h1").TextContent));
        Assert.Equal("CLAUDE.md already exists, so it wasn't created.", screen.Find(".instruction-file-bar .edit-message").TextContent);
        Assert.Empty(screen.FindAll("textarea"));
        Assert.Equal("# Mine\n", System.Text.Encoding.UTF8.GetString(_fakes.InstructionFiles.Get(Project, Agent.ClaudeCode, "CLAUDE.md")!));
    }

    [Theory]
    [InlineData(InstructionFileCreateFailure.AccessDenied)]
    [InlineData(InstructionFileCreateFailure.FolderMissing)]
    [InlineData(InstructionFileCreateFailure.Other)]
    public void Any_other_failure_says_so_under_the_button_and_lets_the_user_try_again(InstructionFileCreateFailure failure)
    {
        var screen = OpenInstructionTab(Render(Project, "api"));
        _fakes.InstructionFileCreation.FailWith(failure);

        screen.Find(".instruction-empty-state .button-primary").Click();

        screen.WaitForAssertion(() => Assert.Equal("CLAUDE.md couldn't be created.", screen.Find(".instruction-empty-state .edit-message").TextContent));
        Assert.False(ButtonOf(screen).HasAttribute("disabled"));

        _fakes.InstructionFileCreation.FailWith(null);
        ButtonOf(screen).Click();
        screen.WaitForAssertion(() => Assert.NotNull(screen.Find("textarea")));
        Assert.Empty(screen.FindAll(".edit-message"));
    }

    [Fact]
    public void The_failure_note_goes_away_when_the_agent_is_switched()
    {
        var screen = OpenInstructionTab(Render(Project, "api"));
        _fakes.InstructionFileCreation.FailWith(InstructionFileCreateFailure.Other);
        screen.Find(".instruction-empty-state .button-primary").Click();
        screen.WaitForAssertion(() => Assert.Single(screen.FindAll(".instruction-empty-state .edit-message")));

        screen.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();

        Assert.Empty(screen.FindAll(".edit-message"));
        Assert.Equal("Create AGENTS.md", screen.Find(".instruction-empty-state .button-primary").TextContent.Trim());
    }

    private static IElement ButtonOf(IRenderedComponent<LocationScreen> screen) => screen.Find(".instruction-empty-state .button-primary");

    private IRenderedComponent<LocationScreen> Render(Location location, string title, Agent agent = Agent.ClaudeCode) =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, location)
            .Add(screen => screen.Title, title)
            .Add(screen => screen.Agent, agent));

    private static IRenderedComponent<LocationScreen> OpenInstructionTab(IRenderedComponent<LocationScreen> screen)
    {
        screen.FindAll("[role=tab]")[1].Click();

        return screen;
    }
}
