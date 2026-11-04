using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class InstructionFileTabTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly Location Personal = new Location.Personal();

    private readonly UiFakes _fakes = new();

    public InstructionFileTabTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_one_file_with_its_project_relative_path_and_its_markdown()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Project\n\nCore **service**.\n"));

        var screen = OpenInstructionTab(RenderProject());

        Assert.Equal("./CLAUDE.md", screen.Find(".instruction-file-path").TextContent);
        Assert.Equal("Project", screen.Find(".instruction-file .markdown h1").TextContent);
        Assert.Equal("service", screen.Find(".instruction-file .markdown strong").TextContent);
    }

    [Fact]
    public void Shows_two_files_in_order_separated_by_sections()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Root\n"),
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/work/api/.claude", "# Nested\n"));

        var screen = OpenInstructionTab(RenderProject());

        Assert.Equal(["./CLAUDE.md", "./.claude/CLAUDE.md"], screen.FindAll(".instruction-file-path").Select(path => path.TextContent));
        Assert.Equal(["Root", "Nested"], screen.FindAll(".instruction-file h1").Select(heading => heading.TextContent));
        Assert.Equal(2, screen.FindAll(".instruction-file").Count);
    }

    [Fact]
    public void A_personal_file_is_labelled_relative_to_the_home_directory()
    {
        _fakes.InstructionFiles.Add(Personal, Agent.ClaudeCode,
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/home/me/.claude", "# Mine\n"));

        var screen = OpenInstructionTab(RenderPersonal());

        Assert.Equal("~/.claude/CLAUDE.md", screen.Find(".instruction-file-path").TextContent);
    }

    [Theory]
    [InlineData(false, Agent.ClaudeCode, "No CLAUDE.md in this project")]
    [InlineData(false, Agent.Codex, "No AGENTS.md in this project")]
    [InlineData(true, Agent.ClaudeCode, "No personal CLAUDE.md")]
    [InlineData(true, Agent.Codex, "No personal AGENTS.md")]
    public void Without_a_file_it_says_so_in_the_words_of_the_location(bool personal, Agent agent, string expected)
    {
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, personal ? Personal : Project)
            .Add(screen => screen.Title, personal ? "Personal" : "api")
            .Add(screen => screen.Agent, agent));

        OpenInstructionTab(screen);

        Assert.Equal(expected, screen.Find(".instruction-empty").TextContent);
        Assert.Empty(screen.FindAll(".instruction-file"));
    }

    [Fact]
    public void Show_in_folder_asks_the_port_to_reveal_the_folder_of_that_file()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Root\n"),
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/work/api/.claude", "# Nested\n"));
        var screen = OpenInstructionTab(RenderProject());

        screen.FindAll(".instruction-file .text-action").Where(action => action.TextContent.Trim() == "Show in folder").ElementAt(1).Click();

        Assert.Equal(["/work/api/.claude"], _fakes.Revealer.Shown);
    }

    [Fact]
    public void A_file_that_cannot_be_shown_gets_the_calm_message_and_keeps_Show_in_folder()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.Binary("CLAUDE.md", "/work/api"));
        var screen = OpenInstructionTab(RenderProject());

        Assert.Equal("This file can't be shown here.", screen.Find(".instruction-file .file-not-shown").TextContent);
        Assert.Empty(screen.FindAll(".markdown"));

        screen.Find(".instruction-file .text-action").Click();

        Assert.Equal(["/work/api"], _fakes.Revealer.Shown);
    }

    [Fact]
    public void Switching_the_agent_keeps_the_instruction_tab_and_shows_the_file_of_the_other_agent()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# For Claude\n"));
        _fakes.InstructionFiles.Add(Project, Agent.Codex, InMemoryInstructionFiles.File("AGENTS.md", "/work/api", "# For Codex\n"));
        var screen = OpenInstructionTab(RenderProject());

        screen.FindAll(".agent-switch button").Single(button => button.TextContent == "Codex").Click();

        Assert.Equal("AGENTS.md", SelectedTab(screen).TextContent);
        Assert.Equal("./AGENTS.md", screen.Find(".instruction-file-path").TextContent);
        Assert.Equal("For Codex", screen.Find(".instruction-file h1").TextContent);
    }

    [Fact]
    public void Hides_the_skill_list_and_Copy_from_while_the_instruction_tab_is_open()
    {
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        var screen = RenderProject();
        Assert.Contains(screen.FindAll("button"), button => button.TextContent == "Copy from…");

        OpenInstructionTab(screen);

        Assert.Empty(screen.FindAll(".skill-list"));
        Assert.DoesNotContain(screen.FindAll("button"), button => button.TextContent == "Copy from…");
    }

    [Fact]
    public void Going_back_to_the_Skills_tab_shows_the_skills_and_Copy_from_again()
    {
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("pdf", "pdf", "Reads PDFs."));
        var screen = OpenInstructionTab(RenderProject());

        screen.FindAll("[role=tab]")[0].Click();

        Assert.Single(screen.FindAll(".skill-row"));
        Assert.Equal("Skills", SelectedTab(screen).TextContent);
        Assert.Contains(screen.FindAll("button"), button => button.TextContent == "Copy from…");
    }

    [Fact]
    public void Shows_nothing_for_the_instruction_tab_while_the_files_are_loading()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Application.Viewing.InstructionFileBytes>>();
        using var context = new BunitContext();
        _fakes.AddTo(context.Services);
        context.Services.AddSingleton(new Application.Viewing.ReadInstructionFiles(new PendingInstructionFiles(pending.Task)));
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var screen = context.Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, Project)
            .Add(screen => screen.Title, "api"));

        screen.FindAll("[role=tab]")[1].Click();

        Assert.Empty(screen.FindAll(".instruction-empty"));

        pending.SetResult([]);
        screen.WaitForAssertion(() => Assert.Single(screen.FindAll(".instruction-empty")));
    }

    private IRenderedComponent<LocationScreen> RenderProject() =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, Project)
            .Add(screen => screen.Title, "api"));

    private IRenderedComponent<LocationScreen> RenderPersonal() =>
        Render<LocationScreen>(parameters => parameters
            .Add(screen => screen.Location, Personal)
            .Add(screen => screen.Title, "Personal"));

    private static IRenderedComponent<LocationScreen> OpenInstructionTab(IRenderedComponent<LocationScreen> screen)
    {
        screen.FindAll("[role=tab]")[1].Click();

        return screen;
    }

    private static AngleSharp.Dom.IElement SelectedTab(IRenderedComponent<LocationScreen> screen) =>
        screen.FindAll("[role=tab]").Single(tab => tab.GetAttribute("aria-selected") == "true");

    private sealed class PendingInstructionFiles(Task<IReadOnlyList<Application.Viewing.InstructionFileBytes>> result)
        : Application.Viewing.IInstructionFiles
    {
        public Task<IReadOnlyList<Application.Viewing.InstructionFileBytes>> ReadAsync(
            Location location, Agent agent, int maxBytes, CancellationToken cancellationToken) => result;
    }
}
