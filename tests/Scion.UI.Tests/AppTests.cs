using Bunit;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class AppTests : BunitContext
{
    private static readonly DateTimeOffset Noon = UiFakes.Noon;

    private readonly UiFakes _fakes = new();
    private readonly InMemoryProjectRegistry _registry;
    private readonly InMemorySkillFolderReader _reader;
    private readonly FakeFolderPicker _picker;

    public AppTests()
    {
        _registry = _fakes.Registry;
        _reader = _fakes.Reader;
        _picker = _fakes.Picker;
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Starts_on_the_project_grid()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));

        var app = Render<App>();

        Assert.Single(app.FindAll(".project-card"));
        Assert.Empty(app.FindAll(".tabs"));
    }

    [Fact]
    public void Opening_a_project_card_shows_the_skills_of_that_project()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("api-conventions", "api-conventions", "Conventions."));
        var app = Render<App>();

        app.Find(".project-open").Click();

        Assert.Equal("api", app.Find("h1").TextContent);
        Assert.Equal(["api-conventions"], app.FindAll(".skill-name").Select(name => name.TextContent));
        Assert.Empty(app.FindAll(".project-card"));
    }

    [Fact]
    public void Opening_personal_shows_the_personal_skills()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddPersonal(Agent.ClaudeCode, new SkillFolder("everywhere", "everywhere", "Everywhere."));
        var app = Render<App>();

        app.Find(".personal-card").Click();

        Assert.Equal("Personal", app.Find("h1").TextContent);
        Assert.Equal(["everywhere"], app.FindAll(".skill-name").Select(name => name.TextContent));
    }

    [Fact]
    public void Adding_a_project_opens_its_screen()
    {
        _picker.Folder = "/work/orders-api";
        var app = Render<App>();

        app.Find(".button-primary").Click();

        Assert.Equal("orders-api", app.Find("h1").TextContent);
        Assert.Equal("/work/orders-api", app.Find(".screen-path").TextContent);
    }

    [Fact]
    public void Going_back_returns_to_the_grid_with_the_opened_project_first()
    {
        _registry.Add(new RegisteredProject("/work/first", "first", Noon.AddDays(-1)));
        _registry.Add(new RegisteredProject("/work/second", "second", Noon.AddDays(-2)));
        var app = Render<App>();
        app.FindAll(".project-open")[1].Click();

        app.Find("button[aria-label=Back]").Click();

        Assert.Equal(["second", "first"], app.FindAll(".project-name").Select(name => name.TextContent));
    }

    [Fact]
    public void Opening_a_project_with_a_missing_folder_shows_the_missing_folder_state()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _fakes.Folders.MarkMissing("/work/gone");
        var app = Render<App>();

        app.Find(".project-open").Click();

        Assert.Equal("This folder no longer exists.", app.Find(".folder-missing-state p").TextContent);
        Assert.Empty(app.FindAll(".agent-switch"));
    }

    [Fact]
    public void Removing_a_project_with_a_missing_folder_goes_back_to_the_grid_without_it()
    {
        _registry.Add(new RegisteredProject("/work/gone", "gone", Noon));
        _registry.Add(new RegisteredProject("/work/api", "api", Noon.AddDays(-1)));
        _fakes.Folders.MarkMissing("/work/gone");
        var app = Render<App>();
        app.FindAll(".project-open")[0].Click();

        app.Find(".button-secondary").Click();

        Assert.Equal(["api"], app.FindAll(".project-name").Select(name => name.TextContent));
    }

    [Fact]
    public void Clicking_a_skill_opens_its_page()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        _fakes.SkillFiles.Add(new Location.Project("/work/api"), Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", "# PDF\n"));
        var app = Render<App>();
        app.Find(".project-open").Click();

        app.Find(".skill-open").Click();

        Assert.Equal("pdf", app.Find("h1").TextContent);
        Assert.Equal("api · Claude", app.Find(".screen-subtitle").TextContent);
        Assert.Empty(app.FindAll(".skill-list"));
    }

    [Fact]
    public void Edit_in_the_row_menu_opens_the_skill_page_already_in_edit_mode()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        _fakes.SkillFiles.Add(new Location.Project("/work/api"), Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", "# PDF\n"));
        var app = Render<App>();
        app.Find(".project-open").Click();
        app.Find(".skill-row .options-button").Click();

        app.FindAll(".skill-row [role=menuitem]").Single(item => item.TextContent.Trim() == "Edit").Click();

        Assert.Equal("pdf", app.Find("h1").TextContent);
        Assert.Equal("# PDF\n", app.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void A_new_skill_opens_its_page_already_editing_the_template()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        var app = Render<App>();
        app.Find(".project-open").Click();
        app.FindAll(".empty-state .button").Single(button => button.TextContent.Trim() == "New skill").Click();
        app.Find("input#new-skill-name").Input("code-review");
        app.Find("textarea#new-skill-description").Input("Reviews pull requests.");

        app.Find(".dialog .button-primary").Click();

        app.WaitForAssertion(() => Assert.Equal("code-review", app.Find("h1").TextContent));
        Assert.Equal(
            "---\nname: code-review\ndescription: Reviews pull requests.\n---\n\n",
            app.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Opening_a_skill_by_clicking_it_does_not_start_editing()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        _fakes.SkillFiles.Add(new Location.Project("/work/api"), Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", "# PDF\n"));
        var app = Render<App>();
        app.Find(".project-open").Click();

        app.Find(".skill-open").Click();

        Assert.Empty(app.FindAll("textarea"));
    }

    [Fact]
    public void Editing_a_skill_does_not_carry_over_to_the_next_one_opened()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        _fakes.SkillFiles.Add(new Location.Project("/work/api"), Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", "# PDF\n"));
        var app = Render<App>();
        app.Find(".project-open").Click();
        app.Find(".skill-row .options-button").Click();
        app.FindAll(".skill-row [role=menuitem]").Single(item => item.TextContent.Trim() == "Edit").Click();
        app.Find("button[aria-label=Back]").Click();

        app.Find(".skill-open").Click();

        Assert.Empty(app.FindAll("textarea"));
    }

    [Fact]
    public void Going_back_from_a_skill_returns_to_the_same_agent_on_the_skills_tab()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.Codex, new SkillFolder("only-codex", "only-codex", "For Codex."));
        _fakes.SkillFiles.Add(new Location.Project("/work/api"), Agent.Codex, "only-codex", "/work/api/.agents/skills/only-codex", ("SKILL.md", "# Codex\n"));
        var app = Render<App>();
        app.Find(".project-open").Click();
        app.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();
        app.Find(".skill-open").Click();
        Assert.Equal("api · Codex", app.Find(".screen-subtitle").TextContent);

        app.Find("button[aria-label=Back]").Click();

        Assert.Equal(["only-codex"], app.FindAll(".skill-name").Select(name => name.TextContent));
        Assert.Equal("true", app.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").GetAttribute("aria-pressed"));
        Assert.Equal("Skills", app.Find("[role=tab][aria-selected=true]").TextContent);
    }

    [Fact]
    public void Opening_another_location_starts_with_claude_again()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _reader.AddProject("/work/api", Agent.Codex, new SkillFolder("only-codex", "only-codex", "For Codex."));
        var app = Render<App>();
        app.Find(".project-open").Click();
        app.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();
        app.Find("button[aria-label=Back]").Click();

        app.Find(".personal-card").Click();

        Assert.Equal("true", app.FindAll(".agent-switch button").Single(button => button.TextContent == "Claude").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void An_unexpected_failure_shows_something_went_wrong_instead_of_a_blank_page()
    {
        _registry.CrashesWith = new InvalidOperationException("boom");

        var app = Render<App>();

        Assert.Equal("Something went wrong", app.Find("h1").TextContent);
        Assert.Equal("Try again", Assert.Single(app.FindAll(".button-secondary")).TextContent.Trim());
        Assert.Empty(app.FindAll(".button-primary"));
        Assert.DoesNotContain("boom", app.Markup);
    }

    [Fact]
    public void Try_again_after_an_unexpected_failure_goes_back_to_the_grid()
    {
        _registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _registry.CrashesWith = new InvalidOperationException("boom");
        var app = Render<App>();
        _registry.CrashesWith = null;

        app.Find(".button-secondary").Click();

        Assert.Equal(["api"], app.FindAll(".project-name").Select(name => name.TextContent));
    }
}
