using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class StartScreenTests : BunitContext
{
    private static readonly DateTimeOffset Noon = UiFakes.Noon;

    private readonly UiFakes _fakes = new();
    private readonly InMemoryProjectRegistry _registry;
    private readonly InMemorySkillFolderReader _reader;
    private readonly FakeFolderPicker _picker;
    private readonly List<OpenedProject> _openedProjects = [];
    private int _personalOpenedCount;

    public StartScreenTests()
    {
        _registry = _fakes.Registry;
        _reader = _fakes.Reader;
        _picker = _fakes.Picker;
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_the_personal_card_first_with_its_skill_counts()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddPersonal(Agent.ClaudeCode, Skill("one"), Skill("two"), Skill("three"));
        _reader.AddPersonal(Agent.Codex, Skill("one"));

        var screen = RenderStartScreen();

        var personal = screen.Find(".personal-card");
        Assert.Equal("Personal", personal.QuerySelector(".personal-name")!.TextContent);
        Assert.Contains("Skills available in every project", personal.TextContent);
        Assert.Equal("Claude 3 · Codex 1", personal.QuerySelector(".skill-counts")!.TextContent);
        Assert.True(personal.CompareDocumentPosition(screen.Find(".project-card")).HasFlag(DocumentPositions.Following));
    }

    [Fact]
    public void Shows_one_card_per_project_in_registry_order_with_avatar_name_path_and_counts()
    {
        _registry.Add(new RegisteredProject("/work/older", "older-project", Noon.AddDays(-1)));
        _registry.Add(new RegisteredProject("/work/orders-api", "orders-api", Noon));
        _reader.AddProject("/work/orders-api", Agent.ClaudeCode, Skill("one"), Skill("two"));
        _reader.AddProject("/work/orders-api", Agent.Codex, Skill("one"));

        var cards = RenderStartScreen().FindAll(".project-card");

        Assert.Equal(2, cards.Count);
        Assert.Equal("orders-api", cards[0].QuerySelector(".project-name")!.TextContent);
        Assert.Equal("/work/orders-api", cards[0].QuerySelector(".project-path")!.TextContent);
        Assert.Equal("OA", cards[0].QuerySelector(".avatar")!.TextContent);
        Assert.Equal("Claude 2 · Codex 1", cards[0].QuerySelector(".skill-counts")!.TextContent);
        Assert.Equal("older-project", cards[1].QuerySelector(".project-name")!.TextContent);
    }

    [Fact]
    public void Shows_a_project_path_inside_the_home_directory_with_a_tilde()
    {
        _registry.Add(new RegisteredProject("/home/me/work/api", "api", Noon));

        var card = RenderStartScreen().Find(".project-card");

        Assert.Equal("~/work/api", card.QuerySelector(".project-path")!.TextContent);
        Assert.Equal("~/work/", card.QuerySelector(".path-head")!.TextContent);
        Assert.Equal("api", card.QuerySelector(".path-tail")!.TextContent);
    }

    [Fact]
    public void Dims_a_zero_count_and_only_a_zero_count()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, Skill("one"));

        var counts = RenderStartScreen().Find(".project-card .skill-counts");

        Assert.Equal(["Codex 0"], counts.QuerySelectorAll(".count-zero").Select(count => count.TextContent));
    }

    [Fact]
    public void Shows_the_wordmark_and_a_secondary_add_project_button_with_a_plus_icon_and_never_the_word_locations()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var screen = RenderStartScreen();

        var addButton = screen.Find(".button-secondary");
        Assert.Equal("Scion", screen.Find(".wordmark").TextContent);
        Assert.Equal("Add project", addButton.TextContent.Trim());
        Assert.NotNull(addButton.QuerySelector("svg.add-icon"));
        Assert.Empty(screen.FindAll(".button-primary"));
        Assert.DoesNotContain("Locations", screen.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shows_the_first_run_entry_with_one_primary_button_and_no_grid_when_there_are_no_projects()
    {
        _reader.AddPersonal(Agent.ClaudeCode, Skill("one"));

        var screen = RenderStartScreen();

        Assert.Equal("scion", screen.Find(".first-run h1").TextContent);
        Assert.Contains("rhymes with lion", screen.Find(".first-run").TextContent);
        Assert.Equal(
            [
                "1.A young shoot cut from one plant to be grafted onto another.",
                "2.A desktop app that grafts your AI skills from one project onto another, so they grow on new roots.",
            ],
            screen.FindAll(".first-run li").Select(definition => definition.TextContent));
        Assert.DoesNotContain("—", screen.Find(".first-run").TextContent);
        Assert.Equal("Add your first project", Assert.Single(screen.FindAll(".button-primary")).TextContent.Trim());
        Assert.Equal("Claude 1 · Codex 0", screen.Find(".personal-card .skill-counts").TextContent);
        Assert.Empty(screen.FindAll(".project-card"));
        Assert.Empty(screen.FindAll(".button-secondary"));
    }

    [Fact]
    public void Shows_nothing_while_the_projects_are_still_loading()
    {
        var pendingRegistry = new TaskCompletionSource<IReadOnlyList<RegisteredProject>>();
        using var context = new BunitContext();
        context.Services.AddSingleton(
            new ListProjects(new PendingProjectRegistry(pendingRegistry.Task), new ListSkills(_reader), _fakes.Folders));
        context.Services.AddSingleton(new RegisterProject(_registry, _fakes.Time));
        context.Services.AddSingleton(new OpenProject(_registry, _fakes.Time, _fakes.Folders));
        context.Services.AddSingleton(new RemoveProject(_registry));
        context.Services.AddSingleton(new RenameProject(_registry));
        context.Services.AddSingleton(new StartFresh(_fakes.Recovery, _fakes.Time));
        context.Services.AddSingleton<IFolderPicker>(_picker);
        context.Services.AddSingleton(new UserHome(UiFakes.HomeDirectory, StringComparison.Ordinal));

        var screen = context.Render<StartScreen>();

        Assert.Empty(screen.FindAll(".first-run"));

        pendingRegistry.SetResult([]);
        screen.WaitForAssertion(() => Assert.Single(screen.FindAll(".first-run")));
    }

    [Fact]
    public void Adding_a_project_registers_it_and_hands_it_over_to_be_opened()
    {
        _picker.Folder = "/work/orders-api";
        var screen = RenderStartScreen();

        screen.Find(".button-primary").Click();

        Assert.Equal([new OpenedProject("/work/orders-api", "orders-api")], _openedProjects);
        Assert.Equal([new RegisteredProject("/work/orders-api", "orders-api", Noon)], _registry.Projects);
    }

    [Fact]
    public void Adding_a_project_from_the_grid_works_the_same_way()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon.AddDays(-1)));
        _picker.Folder = "/work/web";
        var screen = RenderStartScreen();

        screen.Find(".button-secondary").Click();

        Assert.Equal([new OpenedProject("/work/web", "web")], _openedProjects);
        Assert.Equal(2, _registry.Projects.Count);
    }

    [Fact]
    public void Adding_a_folder_that_is_already_registered_opens_it_without_a_second_entry()
    {
        _registry.Add(new RegisteredProject("/work/api", "Our API", Noon.AddDays(-1)));
        _picker.Folder = "/work/api";
        var screen = RenderStartScreen();

        screen.Find(".button-secondary").Click();

        Assert.Equal([new OpenedProject("/work/api", "Our API")], _openedProjects);
        Assert.Single(_registry.Projects);
    }

    [Fact]
    public void Cancelling_the_folder_picker_changes_nothing()
    {
        _picker.Folder = null;
        var screen = RenderStartScreen();

        screen.Find(".button-primary").Click();

        Assert.Equal(1, _picker.PickCount);
        Assert.Empty(_openedProjects);
        Assert.Empty(_registry.Projects);
    }

    [Fact]
    public void Clicking_a_project_card_opens_that_project_and_marks_it_as_last_opened()
    {
        _registry.Add(new RegisteredProject("/work/api", "Our API", Noon.AddDays(-3)));
        var screen = RenderStartScreen();

        screen.Find(".project-open").Click();

        Assert.Equal([new OpenedProject("/work/api", "Our API")], _openedProjects);
        Assert.Equal(Noon, Assert.Single(_registry.Projects).LastOpenedAt);
    }

    [Fact]
    public void Clicking_a_card_of_a_project_that_is_no_longer_registered_refreshes_the_list_instead_of_opening_it()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _registry.Add(new RegisteredProject("/work/api", "api", Noon.AddDays(-1)));
        var screen = RenderStartScreen();
        _registry.Remove("/work/gone");

        screen.Find(".project-open").Click();

        Assert.Empty(_openedProjects);
        Assert.Equal(["api"], screen.FindAll(".project-name").Select(name => name.TextContent));
    }

    [Fact]
    public void Clicking_the_personal_card_opens_personal_without_touching_the_registry()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon.AddDays(-3)));
        var screen = RenderStartScreen();

        screen.Find(".personal-card").Click();

        Assert.Equal(1, _personalOpenedCount);
        Assert.Empty(_openedProjects);
        Assert.Equal(Noon.AddDays(-3), Assert.Single(_registry.Projects).LastOpenedAt);
    }

    [Fact]
    public void Shows_folder_not_found_with_a_warning_icon_instead_of_the_counts_for_a_missing_folder()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _registry.Add(new RegisteredProject("/work/api", "api", Noon.AddDays(-1)));
        _fakes.Folders.MarkMissing("/work/gone");

        var cards = RenderStartScreen().FindAll(".project-card");

        var missing = cards[0];
        Assert.Equal("Folder not found", missing.QuerySelector(".folder-missing")!.TextContent.Trim());
        Assert.NotNull(missing.QuerySelector(".folder-missing .warning-icon"));
        Assert.Null(missing.QuerySelector(".skill-counts"));
        Assert.Null(cards[1].QuerySelector(".folder-missing"));
        Assert.NotNull(cards[1].QuerySelector(".skill-counts"));
    }

    [Fact]
    public void Opens_a_project_with_a_missing_folder_and_tells_the_caller_about_it()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon.AddDays(-1)));
        _fakes.Folders.MarkMissing("/work/gone");
        var screen = RenderStartScreen();

        screen.Find(".project-open").Click();

        Assert.Equal([new OpenedProject("/work/gone", "gone", FolderMissing: true)], _openedProjects);
    }

    [Fact]
    public void Offers_a_project_options_button_that_is_closed_by_default()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var screen = RenderStartScreen();

        var options = screen.Find(".project-card .options-button");
        Assert.Equal("Project options", options.GetAttribute("aria-label"));
        Assert.Equal("menu", options.GetAttribute("aria-haspopup"));
        Assert.Equal("false", options.GetAttribute("aria-expanded"));
        Assert.Empty(screen.FindAll("[role=menu]"));
    }

    [Fact]
    public void Opens_a_menu_with_rename_and_remove_from_scion_from_the_options_button()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();

        screen.Find(".options-button").Click();

        var menu = screen.Find("[role=menu]");
        Assert.Equal(
            ["Rename", "Remove from Scion"],
            menu.QuerySelectorAll("[role=menuitem]").Select(item => item.TextContent.Trim()));
        Assert.Equal("true", screen.Find(".options-button").GetAttribute("aria-expanded"));
        Assert.Empty(_openedProjects);
    }

    [Fact]
    public void Closes_the_menu_with_escape_and_gives_the_focus_back_to_the_options_button()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        screen.Find(".options-button").Click();

        screen.Find("[role=menu]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(screen.FindAll("[role=menu]"));
        Assert.Equal("false", screen.Find(".options-button").GetAttribute("aria-expanded"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Closes_the_menu_when_the_backdrop_is_clicked()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        screen.Find(".options-button").Click();

        screen.Find(".menu-backdrop").Click();

        Assert.Empty(screen.FindAll("[role=menu]"));
        Assert.Empty(screen.FindAll(".menu-backdrop"));
    }

    [Fact]
    public void Remove_from_scion_unregisters_the_project_at_once_and_its_card_disappears()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _registry.Add(new RegisteredProject("/work/web", "web", Noon.AddDays(-1)));
        var screen = RenderStartScreen();

        MenuItem(screen, "Remove from Scion").Click();

        Assert.Equal(["web"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Equal(["/work/web"], _registry.Projects.Select(project => project.Path));
        Assert.Empty(_openedProjects);
    }

    [Fact]
    public void Removing_the_last_project_brings_back_the_first_run_entry()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();

        MenuItem(screen, "Remove from Scion").Click();

        Assert.Single(screen.FindAll(".first-run"));
    }

    [Fact]
    public void Rename_turns_the_name_into_an_input_holding_the_current_name_and_focuses_it()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();

        MenuItem(screen, "Rename").Click();

        var input = screen.Find("input.project-name-input");
        Assert.Equal("api", input.GetAttribute("value"));
        Assert.Empty(screen.FindAll(".project-name"));
        Assert.Empty(screen.FindAll("[role=menu]"));
        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(input);
    }

    [Fact]
    public void Selects_the_whole_name_when_the_rename_input_gets_the_focus()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();

        MenuItem(screen, "Rename").Click();

        Assert.Equal("this.select()", screen.Find("input.project-name-input").GetAttribute("onfocus"));
    }

    [Fact]
    public void Enter_saves_the_new_name_keeps_the_path_and_puts_the_card_back()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("Our API");

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Our API"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Empty(screen.FindAll("input.project-name-input"));
        Assert.Equal([new RegisteredProject("/work/api", "Our API", Noon)], _registry.Projects);
        Assert.Empty(_openedProjects);
    }

    [Fact]
    public void Leaving_the_input_saves_the_new_name_too()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("Our API");

        input.Blur();

        Assert.Equal(["Our API"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Equal("Our API", Assert.Single(_registry.Projects).DisplayName);
    }

    [Fact]
    public void Escape_cancels_the_rename_and_keeps_the_old_name()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("Our API");

        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(["api"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Equal("api", Assert.Single(_registry.Projects).DisplayName);
    }

    [Fact]
    public void An_empty_name_is_rejected_and_the_old_name_comes_back()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("   ");

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["api"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Equal("api", Assert.Single(_registry.Projects).DisplayName);
    }

    [Fact]
    public void A_name_with_spaces_around_it_is_saved_trimmed()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("  Our API  ");

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Our API"], screen.FindAll(".project-name").Select(name => name.TextContent));
    }

    [Fact]
    public void The_input_does_not_open_the_project_when_it_is_clicked()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();

        Assert.Empty(screen.FindAll(".project-open"));
        Assert.Empty(_openedProjects);
    }

    [Fact]
    public void Shows_the_unreadable_error_screen_with_the_registry_path_instead_of_the_grid()
    {
        _registry.IsUnreadable = true;

        var screen = RenderStartScreen();

        Assert.Equal("Scion couldn't read your project list", screen.Find("h1").TextContent);
        Assert.Equal("~/.scion/projects.json", screen.Find(".error-path").TextContent);
        Assert.Equal("Your old file is kept as a backup.", screen.Find(".error-note").TextContent);
        Assert.Equal("Try again", Assert.Single(screen.FindAll(".button-secondary")).TextContent.Trim());
        Assert.Equal("Start fresh", Assert.Single(screen.FindAll(".button-primary")).TextContent.Trim());
        Assert.Empty(screen.FindAll(".personal-card"));
    }

    [Fact]
    public void Try_again_shows_the_grid_once_the_registry_can_be_read()
    {
        _registry.IsUnreadable = true;
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        _registry.IsUnreadable = false;

        screen.Find(".button-secondary").Click();

        Assert.Equal(["api"], screen.FindAll(".project-name").Select(name => name.TextContent));
        Assert.Empty(screen.FindAll(".error-screen"));
    }

    [Fact]
    public void Try_again_keeps_the_error_screen_while_the_registry_is_still_unreadable()
    {
        _registry.IsUnreadable = true;
        var screen = RenderStartScreen();

        screen.Find(".button-secondary").Click();

        Assert.Equal("Scion couldn't read your project list", screen.Find("h1").TextContent);
    }

    [Fact]
    public void Start_fresh_sets_the_old_file_aside_and_shows_the_first_run_entry()
    {
        _registry.IsUnreadable = true;
        var screen = RenderStartScreen();

        screen.Find(".button-primary").Click();

        Assert.Equal(1, _fakes.Recovery.SetAsideCount);
        Assert.Equal("scion", screen.Find(".first-run h1").TextContent);
        Assert.Empty(screen.FindAll(".error-screen"));
    }

    [Fact]
    public void Start_fresh_that_fails_shows_the_save_error_screen()
    {
        _registry.IsUnreadable = true;
        _fakes.Recovery.FailsToWrite = true;
        var screen = RenderStartScreen();

        screen.Find(".button-primary").Click();

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
    }

    [Fact]
    public void Shows_the_save_error_screen_with_only_try_again_when_a_change_cannot_be_saved()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        _registry.FailsToWrite = true;

        MenuItem(screen, "Remove from Scion").Click();

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
        Assert.Equal("~/.scion/projects.json", screen.Find(".error-path").TextContent);
        Assert.Equal("Try again", Assert.Single(screen.FindAll(".button-secondary")).TextContent.Trim());
        Assert.Empty(screen.FindAll(".button-primary"));
        Assert.Empty(screen.FindAll(".error-note"));
    }

    [Fact]
    public void Try_again_after_a_failed_save_reloads_the_grid_with_the_unchanged_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        _registry.FailsToWrite = true;
        MenuItem(screen, "Remove from Scion").Click();
        _registry.FailsToWrite = false;

        screen.Find(".button-secondary").Click();

        Assert.Equal(["api"], screen.FindAll(".project-name").Select(name => name.TextContent));
    }

    [Fact]
    public void A_rename_that_cannot_be_saved_shows_the_save_error_screen()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        MenuItem(screen, "Rename").Click();
        var input = screen.Find("input.project-name-input");
        input.Input("Our API");
        _registry.FailsToWrite = true;

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
    }

    [Fact]
    public void Adding_a_project_that_cannot_be_saved_shows_the_save_error_screen_and_opens_nothing()
    {
        _picker.Folder = "/work/api";
        var screen = RenderStartScreen();
        _registry.FailsToWrite = true;

        screen.Find(".button-primary").Click();

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
        Assert.Empty(_openedProjects);
    }

    [Fact]
    public void Opening_a_project_whose_time_cannot_be_saved_shows_the_save_error_screen()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var screen = RenderStartScreen();
        _registry.FailsToWrite = true;

        screen.Find(".project-open").Click();

        Assert.Equal("Scion couldn't save your project list", screen.Find("h1").TextContent);
        Assert.Empty(_openedProjects);
    }

    private static IElement MenuItem(IRenderedComponent<StartScreen> screen, string text)
    {
        screen.Find(".options-button").Click();

        return screen.FindAll("[role=menuitem]").Single(item => item.TextContent.Trim() == text);
    }

    [Fact]
    public void The_wordmark_opens_the_version_popover_and_there_is_no_update_action_while_nothing_is_on_offer()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var screen = RenderStartScreen();

        Assert.Equal("Scion", screen.Find("button.wordmark").TextContent);
        Assert.Empty(screen.FindAll(".update-action"));
    }

    [Fact]
    public async Task Puts_the_update_action_in_the_header_right_before_Add_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _fakes.UpdateFeed.Answer = new Scion.Application.Updating.UpdateCheck.Available(new Scion.Application.Updating.AvailableUpdate("1.1.0", null));
        await _fakes.UpdateSession.StartupCheckAsync();

        var screen = RenderStartScreen();

        var actions = screen.Find(".start-header-actions");
        Assert.Equal(["Update to 1.1.0", "Add project"], actions.Children.Select(child => child.TextContent.Trim()));
        Assert.Empty(screen.FindAll(".update-banner"));
    }

    [Fact]
    public async Task Shows_the_dialog_about_the_update_once_the_user_opens_it()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _fakes.UpdateFeed.Answer = new Scion.Application.Updating.UpdateCheck.Available(new Scion.Application.Updating.AvailableUpdate("1.1.0", null));
        await _fakes.UpdateSession.StartupCheckAsync();
        var screen = RenderStartScreen();

        screen.Find(".update-action").Click();

        Assert.Equal("Scion 1.1.0 is available", screen.Find(".dialog h2").TextContent);
    }

    [Fact]
    public void The_first_run_entry_has_no_version_popover_and_no_update_action()
    {
        var screen = RenderStartScreen();

        Assert.Empty(screen.FindAll("button.wordmark"));
        Assert.Empty(screen.FindAll(".update-action"));
    }

    private IRenderedComponent<StartScreen> RenderStartScreen() =>
        Render<StartScreen>(parameters => parameters
            .Add(p => p.OnOpenProject, opened => _openedProjects.Add(opened))
            .Add(p => p.OnOpenPersonal, () => _personalOpenedCount++));

    private static SkillFolder Skill(string name) => new(name, name, "A skill.");

    private sealed class PendingProjectRegistry(Task<IReadOnlyList<RegisteredProject>> projects) : IProjectRegistry
    {
        public Task<IReadOnlyList<RegisteredProject>> ListAsync(CancellationToken cancellationToken) => projects;

        public Task<RegisteredProject?> FindByPathAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<RegisteredProject?>(null);

        public Task SaveAsync(RegisteredProject project, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
