using Bunit;
using Microsoft.JSInterop;
using Scion.Application.Editing;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class SkillPageEditingTests : BunitContext
{
    private const string FolderPath = "/work/api/.claude/skills/pdf";
    private const string SkillText = "---\nname: pdf\ndescription: Reads PDFs.\n---\n# PDF skill\n\nBody text.\n";

    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly SkillSummary Pdf = new("pdf", "pdf", "Reads PDFs.", [], CanBeDeleted: true);
    private static readonly EditableFile SkillMd = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md");
    private static readonly EditableFile RunSh = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "scripts/run.sh");

    private readonly UiFakes _fakes = new();
    private int _backed;

    public SkillPageEditingTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.SkillFiles.Add(Project, Agent.ClaudeCode, "pdf", FolderPath,
            ("SKILL.md", SkillText),
            ("scripts/run.sh", "echo one\necho two\n"),
            ("scripts/other.sh", "echo other\n"));
        _fakes.SkillFiles.AddBinary(Project, Agent.ClaudeCode, "pdf", "assets/logo.png", [0x89, 0x50, 0x4E, 0x47, 0, 0, 0]);
    }

    [Fact]
    public void Offers_Edit_as_the_primary_button_beside_Show_in_folder()
    {
        var page = RenderPage();

        var header = page.Find(".skill-page-title");
        Assert.Equal("Show in folder", header.QuerySelector(".button-secondary")!.TextContent.Trim());
        var edit = header.QuerySelector(".button-primary")!;
        Assert.Equal("Edit", edit.TextContent.Trim());
        Assert.NotNull(edit.QuerySelector("svg"));
    }

    [Fact]
    public void Offers_Edit_for_every_text_file()
    {
        var page = RenderPage();

        OpenFile(page, "run.sh");

        Assert.Single(page.FindAll(".skill-page-title .button-primary"));
    }

    [Fact]
    public void Does_not_offer_Edit_for_a_file_that_cannot_be_shown()
    {
        var page = RenderPage();

        OpenFile(page, "logo.png");

        Assert.Empty(page.FindAll(".skill-page-title .button-primary"));
    }

    [Fact]
    public void Edit_opens_the_whole_SKILL_md_as_raw_text_with_Cancel_and_Save_instead_of_Edit_and_Show_in_folder()
    {
        var page = RenderPage();

        StartEditing(page);

        Assert.Equal(SkillText, page.Find("textarea").GetAttribute("value"));
        Assert.Equal(["Cancel", "Save"], page.FindAll(".skill-page-title .button").Select(button => button.TextContent.Trim()));
        Assert.Empty(page.FindAll(".markdown"));
    }

    [Fact]
    public void Says_Unsaved_changes_only_after_the_text_changed()
    {
        var page = RenderPage();
        StartEditing(page);

        Assert.Empty(page.FindAll(".unsaved-changes"));

        Type(page, SkillText + "more\n");

        Assert.Equal("Unsaved changes", page.Find(".unsaved-changes").TextContent);
    }

    [Fact]
    public void SKILL_md_has_the_Edit_Preview_switch_and_other_text_files_only_the_editor()
    {
        var page = RenderPage();
        StartEditing(page);
        Assert.Single(page.FindAll(".edit-switch"));

        Cancel(page);
        OpenFile(page, "run.sh");
        StartEditing(page);

        Assert.Empty(page.FindAll(".edit-switch"));
        Assert.Equal("echo one\necho two\n", page.Find("textarea").GetAttribute("value"));
        Assert.Equal("scripts/run.sh", page.Find(".text-file-path").TextContent);
    }

    [Fact]
    public void Preview_of_a_draft_without_a_body_shows_the_empty_state()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: pdf\ndescription: Reads PDFs.\n---\n\n");

        page.FindAll(".edit-switch button")[1].Click();

        Assert.Equal("This skill has no instructions yet.", page.Find(".skill-no-instructions").TextContent);
        Assert.Empty(page.FindAll(".markdown"));
    }

    [Fact]
    public void Preview_renders_the_draft_as_Markdown_without_its_frontmatter_and_back_keeps_the_draft()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: pdf\ndescription: Reads PDFs.\n---\n# Draft title\n");

        page.FindAll(".edit-switch button")[1].Click();

        Assert.Equal("Draft title", page.Find(".markdown h1").TextContent);
        Assert.DoesNotContain("name: pdf", page.Find(".file-view").TextContent);
        Assert.Empty(page.FindAll("textarea"));

        page.FindAll(".edit-switch button")[0].Click();

        Assert.Equal("---\nname: pdf\ndescription: Reads PDFs.\n---\n# Draft title\n", page.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Save_writes_the_file_returns_to_view_mode_and_shows_the_new_content()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: pdf\ndescription: Reads PDFs.\n---\n# New title\n");

        Save(page);

        Assert.Equal("---\nname: pdf\ndescription: Reads PDFs.\n---\n# New title\n", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal("New title", page.Find(".file-view .markdown h1").TextContent);
        Assert.Equal("Edit", page.Find(".skill-page-title .button-primary").TextContent.Trim());
        Assert.Empty(page.FindAll(".unsaved-changes"));
    }

    [Fact]
    public void Save_keeps_the_BOM_and_the_line_endings_of_the_file()
    {
        _fakes.SkillFiles.Set(Project, Agent.ClaudeCode, "pdf", "SKILL.md", [0xEF, 0xBB, 0xBF, .. "# Title\r\nbody\r\n"u8.ToArray()]);
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Changed\nbody\n");

        Save(page);

        Assert.Equal(
            (byte[])[0xEF, 0xBB, 0xBF, .. "# Changed\r\nbody\r\n"u8.ToArray()],
            _fakes.EditableFiles.BytesOf(SkillMd));
    }

    [Fact]
    public void Save_backs_up_the_previous_version()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Changed\n");

        Save(page);

        Assert.Equal([SkillMd], _fakes.Backups.Kept);
    }

    [Fact]
    public void A_save_that_could_not_back_up_still_returns_to_view_mode_and_says_so_next_to_the_buttons()
    {
        _fakes.Backups.Fail();
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Changed\n");

        Save(page);

        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal(
            "Saved, but the previous version couldn't be backed up.",
            page.Find(".skill-page-title .edit-message").TextContent.Trim());
    }

    [Fact]
    public void The_shortcut_is_only_listened_to_while_editing()
    {
        var page = RenderPage();
        Assert.Empty(JSInterop.Invocations["scion.onSaveShortcut"]);

        StartEditing(page);
        Assert.Single(JSInterop.Invocations["scion.onSaveShortcut"]);

        Cancel(page);
        Assert.Single(JSInterop.Invocations["scion.offSaveShortcut"]);
    }

    [Fact]
    public async Task The_shortcut_saves_and_stays_in_edit_mode_with_nothing_left_to_save()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Changed\n");

        await PressSaveShortcutAsync(page);

        Assert.Equal("# Changed\n", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Equal("# Changed\n", EditorDraft.In(page));
        Assert.Empty(page.FindAll(".unsaved-changes"));
        Assert.Equal(["Cancel", "Save"], page.FindAll(".skill-page-title .button").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void A_failed_save_stays_in_edit_mode_with_the_error_next_to_the_buttons_and_the_draft_kept()
    {
        _fakes.EditableFiles.FailWritesWith(FileSaveFailure.AccessDenied);
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Changed\n");

        Save(page);

        Assert.Equal(
            "Couldn't save: access to the file was denied.",
            page.Find(".skill-page-title .edit-message[role=alert]").TextContent.Trim());
        Assert.Equal("# Changed\n", EditorDraft.In(page));
        Assert.Equal("Unsaved changes", page.Find(".unsaved-changes").TextContent);
        Assert.Equal(SkillText, _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Fact]
    public void A_file_changed_on_disk_asks_before_saving_and_cancel_keeps_the_draft()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "# Theirs\n");

        Save(page);

        Assert.Equal("This file changed on disk", page.Find(".dialog h2").TextContent);
        Assert.Equal("SKILL.md was changed outside Scion after you started editing.", page.Find(".dialog-message").TextContent);
        Assert.Equal("# Theirs\n", _fakes.EditableFiles.TextOf(SkillMd));

        ClickDialogButton(page, "Cancel");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Equal("# Mine\n", EditorDraft.In(page));
    }

    [Fact]
    public void Reload_takes_the_text_on_disk_and_stays_in_edit_mode()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "# Theirs\n");
        Save(page);

        ClickDialogButton(page, "Reload");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Equal("# Theirs\n", page.Find("textarea").GetAttribute("value"));
        Assert.Empty(page.FindAll(".unsaved-changes"));
    }

    [Fact]
    public void Overwrite_saves_over_the_changed_file_and_returns_to_view_mode_after_the_button()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "# Theirs\n");
        Save(page);

        ClickDialogButton(page, "Overwrite");

        Assert.Equal("# Mine\n", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal("Mine", page.Find(".file-view .markdown h1").TextContent);
    }

    [Fact]
    public async Task Overwrite_after_the_shortcut_stays_in_edit_mode()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "# Theirs\n");
        await PressSaveShortcutAsync(page);

        ClickDialogButton(page, "Overwrite");

        Assert.Equal("# Mine\n", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Equal("# Mine\n", EditorDraft.In(page));
    }

    [Fact]
    public void A_file_deleted_on_disk_says_so_and_offers_no_reload()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.Delete(SkillMd);

        Save(page);

        Assert.Equal("SKILL.md was deleted outside Scion after you started editing.", page.Find(".dialog-message").TextContent);
        Assert.Equal(["Cancel", "Overwrite"], page.FindAll(".dialog-footer .button").Select(button => button.TextContent));
    }

    [Fact]
    public void Overwrite_writes_a_deleted_file_again()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Mine\n");
        _fakes.EditableFiles.Delete(SkillMd);
        Save(page);

        ClickDialogButton(page, "Overwrite");

        Assert.Equal("# Mine\n", _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Fact]
    public void Cancel_without_changes_returns_to_view_mode_without_asking()
    {
        var page = RenderPage();
        StartEditing(page);

        Cancel(page);

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal("Edit", page.Find(".skill-page-title .button-primary").TextContent.Trim());
    }

    [Fact]
    public void Cancel_with_changes_asks_and_Keep_editing_keeps_the_draft()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");

        Cancel(page);

        Assert.Equal("Discard your changes?", page.Find(".dialog h2").TextContent);
        Assert.Equal("Your changes to SKILL.md haven't been saved.", page.Find(".dialog-message").TextContent);

        ClickDialogButton(page, "Keep editing");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Equal("# Draft\n", EditorDraft.In(page));
    }

    [Fact]
    public void Discarding_returns_to_view_mode_and_leaves_the_file_untouched()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");
        Cancel(page);

        ClickDialogButton(page, "Discard changes");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal("PDF skill", page.Find(".file-view .markdown h1").TextContent);
        Assert.Equal(SkillText, _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Fact]
    public void Back_with_changes_asks_first_and_Keep_editing_stays()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");

        page.Find("button[aria-label=Back]").Click();

        Assert.Equal(0, _backed);
        ClickDialogButton(page, "Keep editing");
        Assert.Equal(0, _backed);
        Assert.NotEmpty(page.FindAll("textarea"));
    }

    [Fact]
    public void Back_with_changes_goes_back_after_discarding()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");
        page.Find("button[aria-label=Back]").Click();

        ClickDialogButton(page, "Discard changes");

        Assert.Equal(1, _backed);
    }

    [Fact]
    public void Back_without_changes_goes_back_at_once_even_while_editing()
    {
        var page = RenderPage();
        StartEditing(page);

        page.Find("button[aria-label=Back]").Click();

        Assert.Equal(1, _backed);
        Assert.Empty(page.FindAll(".dialog"));
    }

    [Fact]
    public void Choosing_another_file_with_changes_asks_first()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");

        OpenFile(page, "other.sh");

        Assert.Equal("Discard your changes?", page.Find(".dialog h2").TextContent);
        ClickDialogButton(page, "Keep editing");
        Assert.Equal("SKILL.md", Assert.Single(page.FindAll(".file-tree-file[aria-current=true]")).TextContent.Trim());
        Assert.Equal("# Draft\n", EditorDraft.In(page));
    }

    [Fact]
    public void Choosing_another_file_after_discarding_shows_it_in_view_mode()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");
        OpenFile(page, "other.sh");

        ClickDialogButton(page, "Discard changes");

        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal(["echo other"], page.FindAll(".line-text").Select(line => line.TextContent));
        Assert.Equal("Edit", page.Find(".skill-page-title .button-primary").TextContent.Trim());
    }

    [Fact]
    public void Choosing_another_file_without_changes_leaves_edit_mode_at_once()
    {
        var page = RenderPage();
        StartEditing(page);

        OpenFile(page, "other.sh");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal(["echo other"], page.FindAll(".line-text").Select(line => line.TextContent));
    }

    [Fact]
    public void Choosing_the_file_being_edited_changes_nothing()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");

        OpenFile(page, "SKILL.md");

        Assert.Empty(page.FindAll(".dialog"));
        Assert.Equal("# Draft\n", EditorDraft.In(page));
    }

    [Fact]
    public void The_header_checks_the_draft_live_like_the_skill_list_does()
    {
        var page = RenderPage();
        StartEditing(page);

        Type(page, "---\nname: PDF\ndescription: A new description.\n---\n# Body\n");
        PauseTyping(page);

        Assert.Equal("A new description.", page.Find(".skill-page-description").TextContent);
        Assert.Equal(
            ["Name must use lowercase letters, numbers and hyphens."],
            page.FindAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void The_header_warns_when_the_draft_loses_its_description()
    {
        var page = RenderPage();
        StartEditing(page);

        Type(page, "---\nname: pdf\n---\n# Body\n");
        PauseTyping(page);

        Assert.Equal("Description unavailable", page.Find(".skill-page-description").TextContent);
        Assert.Single(page.FindAll(".skill-warning"));
    }

    [Fact]
    public void The_header_waits_for_a_pause_in_typing_before_it_checks_the_draft()
    {
        var page = RenderPage();
        StartEditing(page);

        Type(page, "---\nname: PDF\ndescription: A new description.\n---\n");
        _fakes.Time.Advance(TimeSpan.FromMilliseconds(249));

        Assert.Equal("Reads PDFs.", page.Find(".skill-page-description").TextContent);
        Assert.Empty(page.FindAll(".skill-warning"));

        _fakes.Time.Advance(TimeSpan.FromMilliseconds(1));

        page.WaitForAssertion(() => Assert.Equal("A new description.", page.Find(".skill-page-description").TextContent));
        Assert.Single(page.FindAll(".skill-warning"));
    }

    [Fact]
    public void Typing_again_restarts_the_wait_and_only_the_last_draft_is_checked()
    {
        var page = RenderPage();
        StartEditing(page);
        var before = _fakes.FrontmatterParser.Calls;

        Type(page, "---\nname: PDF\ndescription: First.\n---\n");
        _fakes.Time.Advance(TimeSpan.FromMilliseconds(200));
        Type(page, "---\nname: pdf\ndescription: Second.\n---\n");
        _fakes.Time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal("Reads PDFs.", page.Find(".skill-page-description").TextContent);

        _fakes.Time.Advance(TimeSpan.FromMilliseconds(50));

        page.WaitForAssertion(() => Assert.Equal("Second.", page.Find(".skill-page-description").TextContent));
        Assert.Equal(before + 1, _fakes.FrontmatterParser.Calls);
    }

    [Fact]
    public void A_checked_draft_is_not_checked_again_when_the_page_renders_again()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: pdf\ndescription: Second.\n---\n");
        PauseTyping(page);
        var checkedOnce = _fakes.FrontmatterParser.Calls;

        page.FindAll(".edit-switch button").Single(button => button.TextContent == "Preview").Click();
        page.FindAll(".edit-switch button").Single(button => button.TextContent == "Edit").Click();

        Assert.Equal(checkedOnce, _fakes.FrontmatterParser.Calls);
    }

    [Fact]
    public void Saving_shows_the_saved_header_at_once_without_waiting_for_a_pause()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: renamed\ndescription: Saved.\n---\n");

        Save(page);

        Assert.Equal("Saved.", page.Find(".skill-page-description").TextContent);
    }

    [Fact]
    public void A_draft_that_was_discarded_is_never_checked_late()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: PDF\ndescription: Draft only.\n---\n");
        Cancel(page);
        ClickDialogButton(page, "Discard changes");
        var checkedBefore = _fakes.FrontmatterParser.Calls;

        PauseTyping(page);

        Assert.Equal(checkedBefore, _fakes.FrontmatterParser.Calls);
        Assert.Equal("Reads PDFs.", page.Find(".skill-page-description").TextContent);
    }

    [Fact]
    public void Closing_the_window_with_a_dirty_draft_is_held_and_asks_to_discard()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");

        var held = _fakes.CloseGuard.TryHoldClose();

        Assert.True(held);
        page.WaitForAssertion(() => Assert.Equal("Discard your changes?", page.Find(".dialog h2").TextContent));
        Assert.Equal(0, _fakes.WindowCloser.Closed);
    }

    [Fact]
    public async Task Discarding_after_a_held_close_closes_the_window()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");
        _fakes.CloseGuard.TryHoldClose();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".dialog")));

        ClickDialogButton(page, "Discard changes");

        await _fakes.WindowCloser.WhenClosed;
        Assert.Equal(1, _fakes.WindowCloser.Closed);
    }

    [Fact]
    public void Keeping_the_draft_after_a_held_close_leaves_the_window_open_and_the_draft_alone()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "# Draft\n");
        _fakes.CloseGuard.TryHoldClose();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".dialog")));

        ClickDialogButton(page, "Keep editing");

        Assert.Equal(0, _fakes.WindowCloser.Closed);
        Assert.Equal("# Draft\n", EditorDraft.In(page));
    }

    [Fact]
    public void Closing_the_window_is_not_held_while_the_draft_is_clean_or_nothing_is_edited()
    {
        var page = RenderPage();
        Assert.False(_fakes.CloseGuard.TryHoldClose());

        StartEditing(page);
        Assert.False(_fakes.CloseGuard.TryHoldClose());

        Type(page, "# Draft\n");
        Cancel(page);
        ClickDialogButton(page, "Discard changes");
        Assert.False(_fakes.CloseGuard.TryHoldClose());
    }

    [Fact]
    public void Closing_the_window_is_not_held_after_the_draft_was_saved()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: pdf\ndescription: Saved.\n---\n");
        Save(page);

        Assert.False(_fakes.CloseGuard.TryHoldClose());
    }

    [Fact]
    public void The_warnings_go_away_when_the_draft_is_fixed()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: PDF\ndescription: x\n---\n");
        PauseTyping(page);

        Type(page, "---\nname: pdf\ndescription: x\n---\n");
        PauseTyping(page);

        Assert.Empty(page.FindAll(".skill-warning"));
    }

    [Fact]
    public void The_title_stays_while_typing_and_follows_the_saved_name()
    {
        var page = RenderPage();
        StartEditing(page);

        Type(page, "---\nname: renamed\ndescription: x\n---\n");
        PauseTyping(page);
        Assert.Equal("pdf", page.Find("h1").TextContent);

        Save(page);
        Assert.Equal("renamed", page.Find("h1").TextContent);
        Assert.Equal(
            ["Name doesn't match the folder name \"pdf\"."],
            page.FindAll(".skill-warning").Select(line => line.TextContent));
    }

    [Fact]
    public void Discarding_restores_the_header_of_the_saved_file()
    {
        var page = RenderPage();
        StartEditing(page);
        Type(page, "---\nname: PDF\ndescription: Draft only.\n---\n");
        PauseTyping(page);
        Cancel(page);

        ClickDialogButton(page, "Discard changes");

        Assert.Equal("Reads PDFs.", page.Find(".skill-page-description").TextContent);
        Assert.Empty(page.FindAll(".skill-warning"));
    }

    [Fact]
    public void Editing_another_file_does_not_touch_the_header()
    {
        var page = RenderPage();
        OpenFile(page, "run.sh");
        StartEditing(page);

        Type(page, "name: PDF\n");

        Assert.Equal("Reads PDFs.", page.Find(".skill-page-description").TextContent);
        Assert.Empty(page.FindAll(".skill-warning"));
    }

    [Fact]
    public void Edits_and_saves_a_script_as_plain_text()
    {
        var page = RenderPage();
        OpenFile(page, "run.sh");
        StartEditing(page);
        Type(page, "echo changed\n");

        Save(page);

        Assert.Equal("echo changed\n", _fakes.EditableFiles.TextOf(RunSh));
        Assert.Equal(["echo changed"], page.FindAll(".line-text").Select(line => line.TextContent));
    }

    [Fact]
    public void A_file_that_vanished_cannot_be_edited_and_says_so_next_to_the_buttons()
    {
        var page = RenderPage();
        _fakes.EditableFiles.Delete(SkillMd);

        StartEditing(page);

        Assert.Empty(page.FindAll("textarea"));
        Assert.Equal("Couldn't read the file on disk.", page.Find(".skill-page-title .edit-message").TextContent.Trim());
    }

    [Fact]
    public void Opens_in_edit_mode_when_asked_to_start_editing()
    {
        var page = RenderPage(startEditing: true);

        Assert.Equal(SkillText, page.Find("textarea").GetAttribute("value"));
    }

    private IRenderedComponent<SkillPage> RenderPage(bool startEditing = false) =>
        Render<SkillPage>(parameters => parameters
            .Add(page => page.Location, Project)
            .Add(page => page.LocationTitle, "api")
            .Add(page => page.Agent, Agent.ClaudeCode)
            .Add(page => page.Skill, Pdf)
            .Add(page => page.StartEditing, startEditing)
            .Add(page => page.OnBack, () => _backed++));

    private static void StartEditing(IRenderedComponent<SkillPage> page) =>
        page.Find(".skill-page-title .button-primary").Click();

    private static void Type(IRenderedComponent<SkillPage> page, string text) => page.Find("textarea").Input(text);

    private static void Save(IRenderedComponent<SkillPage> page) => page.Find(".skill-page-title .button-primary").Click();

    // Long enough for the check of the draft to run, as a pause in typing would be.
    private void PauseTyping(IRenderedComponent<SkillPage> page)
    {
        _fakes.Time.Advance(TimeSpan.FromMilliseconds(250));
        page.WaitForState(() => true);
    }

    private static void Cancel(IRenderedComponent<SkillPage> page) =>
        page.Find(".skill-page-title .button-secondary").Click();

    private static void OpenFile(IRenderedComponent<SkillPage> page, string fileName) =>
        page.FindAll(".file-tree-file").Single(file => file.TextContent.Trim() == fileName).Click();

    private static void ClickDialogButton(IRenderedComponent<SkillPage> page, string label) =>
        page.FindAll(".dialog-footer .button").Single(button => button.TextContent == label).Click();

    private async Task PressSaveShortcutAsync(IRenderedComponent<SkillPage> page)
    {
        var reference = (DotNetObjectReference<SaveShortcut>)JSInterop.Invocations["scion.onSaveShortcut"].Last().Arguments[0]!;

        await page.InvokeAsync(() => reference.Value.ReportShortcut());
    }
}
