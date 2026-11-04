using Bunit;
using Microsoft.JSInterop;
using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.UI.Tests;

public class InstructionFileTabEditingTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly Location Personal = new Location.Personal();
    private static readonly EditableFile ClaudeMd = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, "CLAUDE.md");
    private static readonly EditableFile NestedClaudeMd = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, ".claude/CLAUDE.md");

    private readonly UiFakes _fakes = new();
    private int _backed;

    public InstructionFileTabEditingTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Rules\n\nBe kind.\n"));
    }

    [Fact]
    public void Offers_Edit_as_a_text_action_next_to_Show_in_folder_and_no_primary_button()
    {
        var screen = OpenInstructionTab();

        var bar = screen.Find(".instruction-file-bar");
        Assert.Equal(["Show in folder", "Edit"], bar.QuerySelectorAll(".text-action").Select(action => action.TextContent.Trim()));
        Assert.Empty(screen.FindAll(".button-primary"));
    }

    [Fact]
    public void Does_not_offer_Edit_for_a_file_that_cannot_be_shown()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode, InMemoryInstructionFiles.Binary("CLAUDE.md", "/work/api"));

        var screen = OpenInstructionTab();

        Assert.Equal(["Show in folder"], screen.FindAll(".instruction-file-bar .text-action").Select(action => action.TextContent.Trim()));
    }

    [Fact]
    public void Edit_opens_the_raw_text_with_the_switch_Cancel_and_the_one_primary_Save_in_the_bar()
    {
        var screen = OpenInstructionTab();

        Edit(screen, "./CLAUDE.md");

        Assert.Equal("# Rules\n\nBe kind.\n", screen.Find("textarea").GetAttribute("value"));
        var bar = screen.Find(".instruction-file-bar");
        Assert.NotNull(bar.QuerySelector(".edit-switch"));
        Assert.Equal(["Cancel", "Save"], bar.QuerySelectorAll(".button").Select(button => button.TextContent.Trim()));
        Assert.Single(screen.FindAll(".button-primary"));
        Assert.Empty(screen.FindAll(".instruction-file .markdown"));
    }

    [Fact]
    public void Says_Unsaved_changes_after_typing()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");

        screen.Find("textarea").Input("# Rules\nchanged\n");

        Assert.Equal("Unsaved changes", screen.Find(".unsaved-changes").TextContent);
    }

    [Fact]
    public void Preview_renders_the_draft_as_Markdown_and_back_keeps_it()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft heading\n");

        screen.FindAll(".edit-switch button")[1].Click();

        Assert.Equal("Draft heading", screen.Find(".instruction-file .markdown h1").TextContent);
        Assert.Empty(screen.FindAll("textarea"));

        screen.FindAll(".edit-switch button")[0].Click();

        Assert.Equal("# Draft heading\n", screen.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Save_writes_the_file_returns_to_view_mode_and_shows_the_new_Markdown()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# New rules\n");

        Save(screen);

        Assert.Equal("# New rules\n", _fakes.EditableFiles.TextOf(ClaudeMd));
        Assert.Empty(screen.FindAll("textarea"));
        Assert.Equal("New rules", screen.Find(".instruction-file .markdown h1").TextContent);
        Assert.Contains("Edit", screen.FindAll(".instruction-file-bar .text-action").Select(action => action.TextContent.Trim()));
    }

    [Fact]
    public void Save_backs_up_the_previous_version()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# New rules\n");

        Save(screen);

        Assert.Equal([ClaudeMd], _fakes.Backups.Kept);
    }

    [Fact]
    public void A_save_that_could_not_back_up_says_so_next_to_the_file_name_in_view_mode()
    {
        _fakes.Backups.Fail();
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# New rules\n");

        Save(screen);

        Assert.Equal(
            "Saved, but the previous version couldn't be backed up.",
            screen.Find(".instruction-file-bar .edit-message").TextContent.Trim());
    }

    [Fact]
    public async Task The_shortcut_saves_and_stays_in_edit_mode()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Shortcut\n");

        await PressSaveShortcutAsync(screen);

        Assert.Equal("# Shortcut\n", _fakes.EditableFiles.TextOf(ClaudeMd));
        Assert.Equal("# Shortcut\n", EditorDraft.In(screen));
        Assert.Empty(screen.FindAll(".unsaved-changes"));
    }

    [Fact]
    public void The_shortcut_is_only_listened_to_while_editing()
    {
        var screen = OpenInstructionTab();
        Assert.Empty(JSInterop.Invocations["scion.onSaveShortcut"]);

        Edit(screen, "./CLAUDE.md");
        Assert.Single(JSInterop.Invocations["scion.onSaveShortcut"]);

        Cancel(screen);
        Assert.Single(JSInterop.Invocations["scion.offSaveShortcut"]);
    }

    [Fact]
    public void A_failed_save_stays_in_edit_mode_with_the_error_in_the_bar_and_the_draft_kept()
    {
        _fakes.EditableFiles.FailWritesWith(FileSaveFailure.AccessDenied);
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# New rules\n");

        Save(screen);

        Assert.Equal(
            "Couldn't save: access to the file was denied.",
            screen.Find(".instruction-file-bar .edit-message[role=alert]").TextContent.Trim());
        Assert.Equal("# New rules\n", EditorDraft.In(screen));
    }

    [Fact]
    public void A_file_changed_on_disk_asks_and_Overwrite_saves_and_returns_to_view_mode()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(ClaudeMd, "# Theirs\n");

        Save(screen);

        Assert.Equal("CLAUDE.md was changed outside Scion after you started editing.", screen.Find(".dialog-message").TextContent);
        ClickDialogButton(screen, "Overwrite");

        Assert.Equal("# Mine\n", _fakes.EditableFiles.TextOf(ClaudeMd));
        Assert.Empty(screen.FindAll("textarea"));
    }

    [Fact]
    public void Reload_takes_the_text_on_disk()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(ClaudeMd, "# Theirs\n");
        Save(screen);

        ClickDialogButton(screen, "Reload");

        Assert.Equal("# Theirs\n", screen.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Cancel_without_changes_returns_to_view_mode_without_asking()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");

        Cancel(screen);

        Assert.Empty(screen.FindAll(".dialog"));
        Assert.Empty(screen.FindAll("textarea"));
    }

    [Fact]
    public void Cancel_with_changes_asks_and_discarding_restores_the_view()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");
        Cancel(screen);

        Assert.Equal("Your changes to CLAUDE.md haven't been saved.", screen.Find(".dialog-message").TextContent);
        ClickDialogButton(screen, "Discard changes");

        Assert.Empty(screen.FindAll("textarea"));
        Assert.Equal("Rules", screen.Find(".instruction-file .markdown h1").TextContent);
        Assert.Equal("# Rules\n\nBe kind.\n", _fakes.EditableFiles.TextOf(ClaudeMd));
    }

    [Fact]
    public void With_two_files_only_the_one_being_edited_is_edited_and_the_other_stays_visible_without_Edit()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Root\n"),
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/work/api/.claude", "# Nested\n"));
        var screen = OpenInstructionTab();

        Edit(screen, "./.claude/CLAUDE.md");

        Assert.Equal("# Nested\n", screen.Find("textarea").GetAttribute("value"));
        Assert.Equal("Root", screen.Find(".instruction-file .markdown h1").TextContent);
        var sections = screen.FindAll(".instruction-file");
        Assert.DoesNotContain(
            "Edit",
            sections[0].QuerySelectorAll(".text-action").Select(action => action.TextContent.Trim()));
    }

    [Fact]
    public void Edits_the_nested_file_with_its_own_path()
    {
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/work/api/.claude", "# Nested\n"));
        var screen = OpenInstructionTab();
        Edit(screen, "./.claude/CLAUDE.md");
        screen.Find("textarea").Input("# Changed\n");

        Save(screen);

        Assert.Equal("# Changed\n", _fakes.EditableFiles.TextOf(NestedClaudeMd));
    }

    [Fact]
    public void Edits_a_personal_file()
    {
        _fakes.InstructionFiles.Add(Personal, Agent.ClaudeCode,
            InMemoryInstructionFiles.File(".claude/CLAUDE.md", "/home/me/.claude", "# Mine\n"));
        var screen = RenderPersonal();
        screen.FindAll("[role=tab]")[1].Click();

        Edit(screen, "~/.claude/CLAUDE.md");
        screen.Find("textarea").Input("# Mine, changed\n");
        Save(screen);

        Assert.Equal(
            "# Mine, changed\n",
            _fakes.EditableFiles.TextOf(new EditableFile.InstructionFile(Personal, Agent.ClaudeCode, ".claude/CLAUDE.md")));
    }

    [Fact]
    public void A_file_that_vanished_cannot_be_edited_and_says_so_in_the_bar()
    {
        var screen = OpenInstructionTab();
        _fakes.EditableFiles.Delete(ClaudeMd);

        Edit(screen, "./CLAUDE.md");

        Assert.Empty(screen.FindAll("textarea"));
        Assert.Equal("Couldn't read the file on disk.", screen.Find(".instruction-file-bar .edit-message").TextContent.Trim());
    }

    [Fact]
    public void Switching_agent_with_changes_asks_first_and_Keep_editing_stays_on_the_same_agent()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");

        ClickAgent(screen, "Codex");

        Assert.Equal("Discard your changes?", screen.Find(".dialog h2").TextContent);
        ClickDialogButton(screen, "Keep editing");
        Assert.Equal("Claude", PressedAgent(screen));
        Assert.Equal("# Draft\n", EditorDraft.In(screen));
    }

    [Fact]
    public void Switching_agent_after_discarding_leaves_edit_mode_and_switches()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");
        ClickAgent(screen, "Codex");

        ClickDialogButton(screen, "Discard changes");

        Assert.Equal("Codex", PressedAgent(screen));
        Assert.Empty(screen.FindAll("textarea"));
        Assert.Equal("No AGENTS.md in this project", screen.Find(".instruction-empty").TextContent);
    }

    [Fact]
    public void Switching_agent_without_changes_needs_no_question()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");

        ClickAgent(screen, "Codex");

        Assert.Empty(screen.FindAll(".dialog"));
        Assert.Equal("Codex", PressedAgent(screen));
        Assert.Empty(screen.FindAll("textarea"));
    }

    [Fact]
    public void Changing_tab_with_changes_asks_first()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");

        screen.FindAll("[role=tab]")[0].Click();

        Assert.Equal("Discard your changes?", screen.Find(".dialog h2").TextContent);
        ClickDialogButton(screen, "Keep editing");
        Assert.Equal("true", screen.FindAll("[role=tab]")[1].GetAttribute("aria-selected"));

        screen.FindAll("[role=tab]")[0].Click();
        ClickDialogButton(screen, "Discard changes");

        Assert.Equal("true", screen.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));
        Assert.Empty(screen.FindAll("textarea"));
    }

    [Fact]
    public void Back_with_changes_asks_first_and_goes_back_only_after_discarding()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");

        screen.Find("button[aria-label=Back]").Click();
        Assert.Equal(0, _backed);
        ClickDialogButton(screen, "Keep editing");
        Assert.Equal(0, _backed);

        screen.Find("button[aria-label=Back]").Click();
        ClickDialogButton(screen, "Discard changes");

        Assert.Equal(1, _backed);
    }

    [Fact]
    public void Back_without_changes_goes_back_at_once()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");

        screen.Find("button[aria-label=Back]").Click();

        Assert.Equal(1, _backed);
        Assert.Empty(screen.FindAll(".dialog"));
    }

    [Fact]
    public void Back_with_nothing_being_edited_goes_back_at_once()
    {
        var screen = OpenInstructionTab();

        screen.Find("button[aria-label=Back]").Click();

        Assert.Equal(1, _backed);
    }

    [Fact]
    public void Closing_the_window_with_a_dirty_draft_is_held_and_Discard_closes_it()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");

        Assert.True(_fakes.CloseGuard.TryHoldClose());
        screen.WaitForAssertion(() => Assert.Equal("Discard your changes?", screen.Find(".dialog h2").TextContent));
        Assert.Equal(0, _fakes.WindowCloser.Closed);

        ClickDialogButton(screen, "Discard changes");

        screen.WaitForAssertion(() => Assert.Equal(1, _fakes.WindowCloser.Closed));
    }

    [Fact]
    public void Keeping_the_draft_after_a_held_close_leaves_the_window_open()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        screen.Find("textarea").Input("# Draft\n");
        _fakes.CloseGuard.TryHoldClose();
        screen.WaitForAssertion(() => Assert.NotEmpty(screen.FindAll(".dialog")));

        ClickDialogButton(screen, "Keep editing");

        Assert.Equal(0, _fakes.WindowCloser.Closed);
        Assert.Equal("# Draft\n", EditorDraft.In(screen));
    }

    [Fact]
    public void Closing_the_window_is_not_held_when_the_draft_is_clean_or_editing_ended()
    {
        var screen = OpenInstructionTab();
        Edit(screen, "./CLAUDE.md");
        Assert.False(_fakes.CloseGuard.TryHoldClose());

        screen.Find("textarea").Input("# Draft\n");
        Cancel(screen);
        ClickDialogButton(screen, "Discard changes");

        Assert.False(_fakes.CloseGuard.TryHoldClose());
    }

    private IRenderedComponent<LocationScreen> OpenInstructionTab()
    {
        var screen = Render<LocationScreen>(parameters => parameters
            .Add(page => page.Location, Project)
            .Add(page => page.Title, "api")
            .Add(page => page.OnBack, () => _backed++));
        screen.FindAll("[role=tab]")[1].Click();

        return screen;
    }

    private IRenderedComponent<LocationScreen> RenderPersonal() =>
        Render<LocationScreen>(parameters => parameters
            .Add(page => page.Location, Personal)
            .Add(page => page.Title, "Personal")
            .Add(page => page.OnBack, () => _backed++));

    private static void Edit(IRenderedComponent<LocationScreen> screen, string pathLabel) =>
        screen.FindAll(".instruction-file")
            .Single(section => section.QuerySelector(".instruction-file-path")!.TextContent == pathLabel)
            .QuerySelectorAll(".text-action")
            .Single(action => action.TextContent.Trim() == "Edit")
            .Click();

    private static void Save(IRenderedComponent<LocationScreen> screen) => screen.Find(".instruction-file-bar .button-primary").Click();

    private static void Cancel(IRenderedComponent<LocationScreen> screen) => screen.Find(".instruction-file-bar .button-secondary").Click();

    private static void ClickDialogButton(IRenderedComponent<LocationScreen> screen, string label) =>
        screen.FindAll(".dialog-footer .button").Single(button => button.TextContent == label).Click();

    private static void ClickAgent(IRenderedComponent<LocationScreen> screen, string name) =>
        screen.FindAll(".agent-switch button").Single(button => button.TextContent == name).Click();

    private static string PressedAgent(IRenderedComponent<LocationScreen> screen) =>
        screen.FindAll(".agent-switch button").Single(button => button.GetAttribute("aria-pressed") == "true").TextContent;

    private async Task PressSaveShortcutAsync(IRenderedComponent<LocationScreen> screen)
    {
        var reference = (DotNetObjectReference<SaveShortcut>)JSInterop.Invocations["scion.onSaveShortcut"].Last().Arguments[0]!;

        await screen.InvokeAsync(() => reference.Value.ReportShortcut());
    }
}
