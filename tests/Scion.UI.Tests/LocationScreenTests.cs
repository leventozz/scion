using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class LocationScreenTests : BunitContext
{
    private readonly UiFakes _fakes = new();
    private readonly InMemorySkillFolderReader _reader;

    public LocationScreenTests()
    {
        _reader = _fakes.Reader;
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_the_personal_header()
    {
        var screen = RenderPersonal();

        Assert.Equal("Personal", screen.Find("h1").TextContent);
        Assert.Contains("Skills available in every project", screen.Markup);
    }

    [Fact]
    public void Lists_the_claude_skills_with_name_and_description_in_the_order_the_use_case_returns_them()
    {
        _reader.AddPersonal(
            Agent.ClaudeCode,
            new SkillFolder("zeta", "zeta", "Last one."),
            new SkillFolder("alpha", "alpha", "First one."));

        var screen = RenderPersonal();

        var rows = screen.FindAll(".skill-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal("alpha", rows[0].QuerySelector(".skill-name")!.TextContent);
        Assert.Equal("First one.", rows[0].QuerySelector(".skill-description")!.TextContent);
        Assert.Equal("zeta", rows[1].QuerySelector(".skill-name")!.TextContent);
    }

    [Fact]
    public void Marks_a_skill_with_warnings_with_an_icon_and_one_message_line_per_warning()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("pdf-processing", "Pdf_Tools", "Extracts text."));

        var row = RenderPersonal().Find(".skill-row");

        Assert.NotNull(row.QuerySelector(".warning-icon"));
        Assert.Equal(
            [
                "Name must use lowercase letters, numbers and hyphens.",
            ],
            row.QuerySelectorAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void Shows_one_message_line_for_each_of_a_name_and_a_description_warning()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker-compose", "Docker--Compose", ""));

        var row = RenderPersonal().Find(".skill-row");

        Assert.Equal(
            [
                "Name must use lowercase letters, numbers and hyphens.",
                "Description is missing.",
            ],
            row.QuerySelectorAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void Shows_a_skill_without_warnings_without_icon_or_message_lines()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker-compose", "docker-compose", "Runs compose files."));

        var row = RenderPersonal().Find(".skill-row");

        Assert.Null(row.QuerySelector(".warning-icon"));
        Assert.Empty(row.QuerySelectorAll(".skill-warning"));
    }

    [Fact]
    public void Shows_description_unavailable_when_the_description_is_empty()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("release-notes", null, null, FrontmatterUnreadable: true));

        var row = RenderPersonal().Find(".skill-row");

        Assert.Equal("Description unavailable", row.QuerySelector(".skill-description")!.TextContent);
        Assert.Equal(
            ["SKILL.md header could not be read."],
            row.QuerySelectorAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void Has_claude_pressed_by_default()
    {
        var screen = RenderPersonal();

        Assert.Equal("true", AgentButton(screen, "Claude").GetAttribute("aria-pressed"));
        Assert.Equal("false", AgentButton(screen, "Codex").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Clicking_codex_lists_the_codex_skills_and_presses_codex()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("for-claude", "for-claude", "Claude."));
        _reader.AddPersonal(Agent.Codex, new SkillFolder("for-codex", "for-codex", "Codex."));
        var screen = RenderPersonal();

        AgentButton(screen, "Codex").Click();

        Assert.Equal(["for-codex"], screen.FindAll(".skill-name").Select(name => name.TextContent));
        Assert.Equal("true", AgentButton(screen, "Codex").GetAttribute("aria-pressed"));
        Assert.Equal("false", AgentButton(screen, "Claude").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Shows_an_empty_state_with_the_claude_skills_path_when_there_are_no_claude_skills()
    {
        var screen = RenderPersonal();

        Assert.Equal("No Claude skills yet", screen.Find(".empty-state-title").TextContent);
        Assert.Equal("~/.claude/skills", screen.Find(".empty-state-path").TextContent);
        Assert.Empty(screen.FindAll(".skill-row"));
    }

    [Fact]
    public void Shows_an_empty_state_with_the_codex_skills_path_when_there_are_no_codex_skills()
    {
        var screen = RenderPersonal();

        AgentButton(screen, "Codex").Click();

        Assert.Equal("No Codex skills yet", screen.Find(".empty-state-title").TextContent);
        Assert.Equal("~/.agents/skills", screen.Find(".empty-state-path").TextContent);
    }

    [Fact]
    public void Does_not_show_the_empty_state_when_there_are_skills()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker-compose", "docker-compose", "Runs compose files."));

        var screen = RenderPersonal();

        Assert.Empty(screen.FindAll(".empty-state"));
    }

    [Fact]
    public void Does_not_show_the_empty_state_while_the_skills_are_still_loading()
    {
        var pendingRead = new TaskCompletionSource<IReadOnlyList<SkillFolder>>();
        using var context = new BunitContext();
        context.Services.AddSingleton(new ListSkills(new PendingSkillFolderReader(pendingRead.Task)));
        context.Services.AddSingleton(new RemoveProject(_fakes.Registry));
        context.Services.AddSingleton(new StartFresh(_fakes.Recovery, _fakes.Time));
        context.Services.AddSingleton(new UserHome(UiFakes.HomeDirectory, StringComparison.Ordinal));

        var screen = context.Render<LocationScreen>(PersonalParameters);

        Assert.Empty(screen.FindAll(".empty-state"));

        pendingRead.SetResult([]);
        screen.WaitForAssertion(() => Assert.Single(screen.FindAll(".empty-state")));
    }

    [Fact]
    public void Has_two_tabs_Skills_and_the_instruction_file_of_the_agent()
    {
        var screen = RenderPersonal();

        Assert.Equal(["Skills", "CLAUDE.md"], screen.FindAll("[role=tab]").Select(tab => tab.TextContent));
        Assert.Equal("true", screen.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));
        Assert.Equal("false", screen.FindAll("[role=tab]")[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void The_instruction_tab_is_named_after_the_file_of_the_selected_agent()
    {
        var screen = RenderPersonal();

        AgentButton(screen, "Codex").Click();

        Assert.Equal(["Skills", "AGENTS.md"], screen.FindAll("[role=tab]").Select(tab => tab.TextContent));
    }

    [Fact]
    public void Clicking_a_skill_row_opens_the_skill_but_the_row_menu_does_not()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        var opened = new List<SkillSummary>();
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Personal())
            .Add(screen => screen.Title, "Personal")
            .Add(screen => screen.OnOpenSkill, skill => opened.Add(skill)));

        screen.Find(".options-button").Click();
        Assert.Empty(opened);

        screen.Find(".skill-open").Click();

        Assert.Equal("pdf", Assert.Single(opened).FolderName);
    }

    [Fact]
    public void Reports_the_selected_agent_to_its_owner_so_it_survives_leaving_the_screen()
    {
        var agents = new List<Agent>();
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Personal())
            .Add(screen => screen.Title, "Personal")
            .Add(screen => screen.AgentChanged, agent => agents.Add(agent)));

        AgentButton(screen, "Codex").Click();

        Assert.Equal([Agent.Codex], agents);
    }

    [Fact]
    public void Starts_with_the_agent_it_is_given()
    {
        _reader.AddPersonal(Agent.Codex, new SkillFolder("only-codex", "only-codex", "Codex."));

        var screen = Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Personal())
            .Add(screen => screen.Title, "Personal")
            .Add(screen => screen.Agent, Agent.Codex));

        Assert.Equal(["only-codex"], screen.FindAll(".skill-name").Select(name => name.TextContent));
        Assert.Equal("true", AgentButton(screen, "Codex").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Shows_the_project_name_path_and_avatar_in_the_header_of_a_project()
    {
        var screen = RenderProject("/work/order-payment-api", "order-payment-api");

        Assert.Equal("order-payment-api", screen.Find("h1").TextContent);
        Assert.Equal("/work/order-payment-api", screen.Find(".screen-path").TextContent);
        Assert.Equal("OP", screen.Find(".avatar").TextContent);
        Assert.DoesNotContain("Skills available in every project", screen.Markup);
    }

    [Fact]
    public void Lists_the_skills_of_the_project_and_not_the_personal_ones()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("personal-one", "personal-one", "Personal."));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("project-one", "project-one", "Project."));

        var screen = RenderProject("/work/api", "api");

        Assert.Equal(["project-one"], screen.FindAll(".skill-name").Select(name => name.TextContent));
    }

    [Fact]
    public void Shows_the_agent_skills_folder_of_the_project_in_the_empty_state()
    {
        var screen = RenderProject("/work/api", "api");

        Assert.Equal("No Claude skills yet", screen.Find(".empty-state-title").TextContent);
        Assert.Equal(".claude/skills", screen.Find(".empty-state-path").TextContent);

        AgentButton(screen, "Codex").Click();

        Assert.Equal(".agents/skills", screen.Find(".empty-state-path").TextContent);
    }

    [Fact]
    public void Back_arrow_hands_control_back_to_the_caller()
    {
        var wentBack = false;
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(p => p.Location, new Location.Project("/work/api"))
            .Add(p => p.Title, "api")
            .Add(p => p.OnBack, () => wentBack = true));

        screen.Find("button[aria-label=Back]").Click();

        Assert.True(wentBack);
    }

    [Fact]
    public void Shows_the_back_arrow_on_the_personal_screen_too()
    {
        Assert.NotNull(RenderPersonal().Find("button[aria-label=Back]"));
    }

    [Fact]
    public void Shows_a_project_path_inside_the_home_directory_with_a_tilde()
    {
        var screen = RenderProject("/home/me/work/api", "api");

        Assert.Equal("~/work/api", screen.Find(".screen-path").TextContent);
    }

    [Fact]
    public void Shows_a_tilde_next_to_the_personal_title()
    {
        Assert.Equal("~", RenderPersonal().Find(".screen-title-home").TextContent);
    }

    [Fact]
    public void Shows_the_missing_folder_state_without_agent_switch_tabs_or_skills()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/gone", "gone", UiFakes.Noon));

        var screen = RenderMissingProject("/work/gone", "gone");

        Assert.Equal("gone", screen.Find("h1").TextContent);
        Assert.Equal("/work/gone", screen.Find(".screen-path").TextContent);
        Assert.Equal("GO", screen.Find(".avatar").TextContent);
        Assert.Equal("This folder no longer exists.", screen.Find(".folder-missing-state p").TextContent);
        Assert.NotNull(screen.Find(".folder-missing-state .folder-off-icon"));
        Assert.Empty(screen.FindAll(".agent-switch"));
        Assert.Empty(screen.FindAll(".tabs"));
        Assert.Empty(screen.FindAll(".empty-state"));
        Assert.Empty(screen.FindAll(".skill-row"));
    }

    [Fact]
    public void Offers_remove_from_scion_as_the_only_secondary_button_of_the_missing_folder_state()
    {
        var screen = RenderMissingProject("/work/gone", "gone");

        Assert.Equal("Remove from Scion", Assert.Single(screen.FindAll(".button-secondary")).TextContent.Trim());
        Assert.Empty(screen.FindAll(".button-primary"));
    }

    [Fact]
    public void Removing_a_project_with_a_missing_folder_unregisters_it_and_goes_back()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/gone", "gone", UiFakes.Noon));
        _fakes.Registry.Add(new RegisteredProject("/work/api", "api", UiFakes.Noon));
        var wentBack = false;
        var screen = RenderMissingProject("/work/gone", "gone", () => wentBack = true);

        screen.Find(".button-secondary").Click();

        Assert.True(wentBack);
        Assert.Equal(["/work/api"], _fakes.Registry.Projects.Select(project => project.Path));
    }

    [Fact]
    public void A_removal_that_cannot_be_saved_shows_the_save_error_screen_and_does_not_go_back()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/gone", "gone", UiFakes.Noon));
        var wentBack = false;
        var screen = RenderMissingProject("/work/gone", "gone", () => wentBack = true);
        _fakes.Registry.FailsToWrite = true;

        screen.Find(".button-secondary").Click();

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
        Assert.False(wentBack);
    }

    [Fact]
    public void Try_again_after_a_failed_removal_removes_the_project_and_goes_back()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/gone", "gone", UiFakes.Noon));
        var wentBack = false;
        var screen = RenderMissingProject("/work/gone", "gone", () => wentBack = true);
        _fakes.Registry.FailsToWrite = true;
        screen.Find(".button-secondary").Click();
        _fakes.Registry.FailsToWrite = false;

        screen.Find(".button-secondary").Click();

        Assert.True(wentBack);
        Assert.Empty(_fakes.Registry.Projects);
    }

    [Fact]
    public void Offers_copy_from_in_the_header_of_a_project_with_skills()
    {
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));

        var screen = RenderProject("/work/api", "api");

        var button = screen.Find(".screen-header .button-secondary");
        Assert.Equal("Copy from…", button.TextContent.Trim());
    }

    [Fact]
    public void Offers_copy_from_in_the_header_of_personal_too()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));

        var screen = RenderPersonal();

        Assert.Equal("Copy from…", screen.Find(".screen-header .button-secondary").TextContent.Trim());
    }

    [Fact]
    public void Leaves_the_header_without_copy_from_when_there_are_no_skills_so_the_empty_state_has_the_only_button()
    {
        var screen = RenderProject("/work/api", "api");

        Assert.Empty(screen.FindAll(".screen-header .button"));
        Assert.Single(screen.FindAll(".button-primary"));
    }

    [Fact]
    public void Copy_from_opens_the_dialog_with_this_place_and_the_selected_agent_as_the_target()
    {
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        _reader.AddProject("/work/api", Agent.Codex, new SkillFolder("other", "other", "Other."));
        var screen = RenderProject("/work/api", "api");

        screen.Find(".screen-header .button-secondary").Click();
        Assert.Equal("api · Claude", screen.Find(".copy-place-row .copy-fixed").TextContent);

        screen.Find("button[aria-label=Close]").Click();
        screen.Find(".agent-switch button:last-child").Click();
        screen.Find(".screen-header .button-secondary").Click();

        Assert.Equal("api · Codex", screen.Find(".copy-place-row .copy-fixed").TextContent);
    }

    [Fact]
    public void Copy_from_on_the_personal_screen_names_the_target_personal()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderPersonal();

        screen.Find(".screen-header .button-secondary").Click();

        Assert.Equal("Personal · Claude", screen.Find(".copy-place-row .copy-fixed").TextContent);
    }

    [Fact]
    public void Closing_the_dialog_removes_it()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderPersonal();
        screen.Find(".screen-header .button-secondary").Click();

        screen.Find("button[aria-label=Close]").Click();

        Assert.Empty(screen.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Copying_into_this_screen_closes_the_dialog_and_lists_the_new_skills()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", UiFakes.Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        _reader.AddProject("/work/web", Agent.ClaudeCode, new SkillFolder("deploy", "deploy", "Deploys."));
        var screen = RenderProject("/work/api", "api");
        screen.Find(".screen-header .button-secondary").Click();
        ChoosePlace(screen, "web");
        screen.FindAll(".text-action").Single(action => action.TextContent.Trim() == "Select all").Click();

        screen.Find(".dialog .button-primary").Click();

        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Equal(["deploy", "docker"], screen.FindAll(".skill-row .skill-name").Select(name => name.TextContent).Order());
    }

    [Fact]
    public void Each_skill_row_has_an_options_menu_with_edit_copy_to_adapt_and_delete()
    {
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject("/work/api", "api");

        var options = screen.Find(".skill-row .options-button");
        Assert.Equal("Options for docker", options.GetAttribute("aria-label"));
        options.Click();

        Assert.Equal(
            ["Edit", "Copy to…", "Adapt with AI…", "Delete"],
            screen.FindAll(".skill-row [role=menuitem]").Select(item => item.TextContent.Trim()));
    }

    [Fact]
    public void Copy_to_opens_the_dialog_with_this_place_as_the_source_and_the_skill_checked()
    {
        _reader.AddProject("/work/api", Agent.ClaudeCode,
            new SkillFolder("docker", "docker", "Runs compose files."), new SkillFolder("testing", "testing", "Runs tests."));
        var screen = RenderProject("/work/api", "api");
        screen.FindAll(".skill-row .options-button")[0].Click();

        screen.FindAll(".skill-row [role=menuitem]").Single(item => item.TextContent.Trim() == "Copy to…").Click();

        Assert.Equal("api · Claude", screen.Find(".copy-place-row .copy-fixed").TextContent);
        Assert.Empty(screen.FindAll(".skill-row [role=menu]"));
        var checkedNames = screen.FindAll(".copy-row")
            .Where(row => row.QuerySelector("input[type=checkbox]")!.HasAttribute("checked"))
            .Select(row => row.QuerySelector(".skill-name")!.TextContent);
        Assert.Equal(["docker"], checkedNames);
    }

    [Fact]
    public void Copy_to_another_project_closes_the_dialog_and_keeps_the_list()
    {
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", UiFakes.Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderProject("/work/api", "api");
        screen.Find(".skill-row .options-button").Click();
        screen.FindAll(".skill-row [role=menuitem]").Single(item => item.TextContent.Trim() == "Copy to…").Click();
        ChoosePlace(screen, "web");

        screen.Find(".dialog .button-primary").Click();

        Assert.Empty(screen.FindAll("[role=dialog]"));
        Assert.Equal(["docker"], screen.FindAll(".skill-row .skill-name").Select(name => name.TextContent));
        Assert.Equal(["docker"], _fakes.SkillFolders.Copied);
    }

    [Fact]
    public void The_empty_state_offers_a_primary_copy_from_button_when_the_other_agent_has_no_skills_either()
    {
        var screen = RenderProject("/work/api", "api");

        var button = screen.Find(".empty-state .button-primary");
        Assert.Equal("Copy from…", button.TextContent.Trim());

        button.Click();

        Assert.Equal("api · Claude", screen.Find(".copy-place-row .copy-fixed").TextContent);
        Assert.Equal("Choose a project", screen.Find(".place-picker-button .place-name").TextContent);
    }

    [Fact]
    public void The_empty_state_offers_to_copy_from_the_other_agent_when_it_has_skills()
    {
        _reader.AddProject("/work/api", Agent.Codex, new SkillFolder("for-codex", "for-codex", "Codex only."));
        var screen = RenderProject("/work/api", "api");

        var button = screen.Find(".empty-state .button-primary");
        Assert.Equal("Copy from Codex", button.TextContent.Trim());

        button.Click();

        Assert.Equal("api · Claude", screen.Find(".copy-place-row .copy-fixed").TextContent);
        Assert.Equal("api", screen.Find(".place-picker-button .place-name").TextContent);
        Assert.Equal("Codex", screen.Find(".copy-place-row .agent-switch [aria-pressed=true]").TextContent);
        Assert.Equal(["for-codex"], screen.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
    }

    [Fact]
    public void The_empty_state_of_codex_offers_to_copy_from_claude_when_claude_has_skills()
    {
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
        var screen = RenderPersonal();
        screen.Find(".agent-switch button:last-child").Click();

        Assert.Equal("Copy from Claude", screen.Find(".empty-state .button-primary").TextContent.Trim());
    }

    [Fact]
    public void The_missing_folder_state_has_no_copy_entry_points()
    {
        var screen = RenderMissingProject("/work/gone", "gone");

        Assert.DoesNotContain(screen.FindAll("button"), button => button.TextContent.Contains("Copy"));
    }

    private static void ChoosePlace(IRenderedComponent<LocationScreen> screen, string name)
    {
        screen.Find(".place-picker-button").Click();
        screen.FindAll(".place-option")
            .Single(option => option.QuerySelector(".place-option-name")!.TextContent == name)
            .Click();
    }

    private IRenderedComponent<LocationScreen> RenderMissingProject(string path, string name, Action? onBack = null) =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Project(path))
            .Add(screen => screen.Title, name)
            .Add(screen => screen.FolderMissing, true)
            .Add(screen => screen.OnBack, () => onBack?.Invoke()));

    private IRenderedComponent<LocationScreen> RenderProject(string path, string name) =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, new Location.Project(path))
            .Add(screen => screen.Title, name));

    private IRenderedComponent<LocationScreen> RenderPersonal() =>
        Render<LocationScreen>(PersonalParameters);

    private static void PersonalParameters(ComponentParameterCollectionBuilder<LocationScreen> parameters) =>
        parameters.Add(screen => screen.Location, new Location.Personal()).Add(screen => screen.Title, "Personal");

    private sealed class PendingSkillFolderReader(Task<IReadOnlyList<SkillFolder>> result) : ISkillFolderReader
    {
        public Task<IReadOnlyList<SkillFolder>> ReadAsync(
            Location location, Agent agent, CancellationToken cancellationToken) => result;
    }

    private static AngleSharp.Dom.IElement AgentButton(IRenderedComponent<LocationScreen> screen, string label) =>
        screen.FindAll(".agent-switch button").Single(button => button.TextContent == label);
}
