using System.Text;
using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.UI.Tests;

public class FileEditingTests
{
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly EditableFile SkillMd = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md");

    private readonly UiFakes _fakes = new();

    public FileEditingTests() =>
        _fakes.SkillFiles.Add(Project, Agent.ClaudeCode, "pdf", "/work/api/.claude/skills/pdf", ("SKILL.md", "line one\nline two\n"));

    private FileEditing NewEditing(EditableFile? file = null) => new(
        new OpenFileForEditing(_fakes.EditableFiles),
        new SaveEditedFile(_fakes.EditableFiles, _fakes.EditableFiles, _fakes.Backups, _fakes.Time),
        file ?? SkillMd);

    private async Task<FileEditing> StartedAsync()
    {
        var editing = NewEditing();
        await editing.StartAsync();

        return editing;
    }

    [Fact]
    public async Task Starts_with_the_text_of_the_file_and_nothing_to_save()
    {
        var editing = NewEditing();

        var started = await editing.StartAsync();

        Assert.True(started);
        Assert.Equal("line one\nline two\n", editing.Draft);
        Assert.False(editing.IsDirty);
        Assert.Equal(EditMode.Edit, editing.Mode);
        Assert.Null(editing.Error);
    }

    [Fact]
    public async Task A_file_that_is_gone_cannot_be_started_and_says_so()
    {
        _fakes.EditableFiles.Delete(SkillMd);
        var editing = NewEditing();

        var started = await editing.StartAsync();

        Assert.False(started);
        Assert.Equal("Couldn't read the file on disk.", editing.Error);
    }

    [Fact]
    public async Task A_file_that_is_not_text_cannot_be_started_and_says_so()
    {
        _fakes.SkillFiles.Set(Project, Agent.ClaudeCode, "pdf", "SKILL.md", [0x89, 0x50, 0x00, 0x47]);
        var editing = NewEditing();

        Assert.False(await editing.StartAsync());
        Assert.Equal("This file can't be edited here.", editing.Error);
    }

    [Fact]
    public async Task Typing_makes_the_draft_dirty_and_typing_it_back_makes_it_clean_again()
    {
        var editing = await StartedAsync();

        editing.SetDraft("changed");
        Assert.True(editing.IsDirty);

        editing.SetDraft("line one\nline two\n");
        Assert.False(editing.IsDirty);
    }

    [Fact]
    public async Task Tells_its_listener_when_something_changes()
    {
        var editing = await StartedAsync();
        var changes = 0;
        editing.Changed += () => changes++;

        editing.SetDraft("changed");
        editing.ShowPreview();

        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Switches_between_the_editor_and_the_preview()
    {
        var editing = await StartedAsync();

        editing.ShowPreview();
        Assert.Equal(EditMode.Preview, editing.Mode);

        editing.ShowEditor();
        Assert.Equal(EditMode.Edit, editing.Mode);
    }

    [Fact]
    public async Task Saving_writes_the_draft_and_leaves_nothing_to_save()
    {
        var editing = await StartedAsync();
        editing.SetDraft("changed\n");

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Equal("changed\n", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.False(editing.IsDirty);
        Assert.Null(editing.Error);
        Assert.Null(editing.Note);
        Assert.False(editing.IsSaving);
    }

    [Fact]
    public async Task Saving_keeps_the_line_endings_and_the_BOM_of_the_file()
    {
        _fakes.SkillFiles.Set(Project, Agent.ClaudeCode, "pdf", "SKILL.md", [0xEF, 0xBB, 0xBF, .. "a\r\nb\r\n"u8.ToArray()]);
        var editing = await StartedAsync();
        editing.SetDraft("a\nchanged\n");

        await editing.SaveAsync();

        Assert.Equal(
            (byte[])[0xEF, 0xBB, 0xBF, .. "a\r\nchanged\r\n"u8.ToArray()],
            _fakes.EditableFiles.BytesOf(SkillMd));
    }

    [Fact]
    public async Task Can_save_again_after_a_save_because_the_baseline_moves_on()
    {
        var editing = await StartedAsync();
        editing.SetDraft("first");
        await editing.SaveAsync();
        editing.SetDraft("second");

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Equal("second", _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Fact]
    public async Task A_save_that_could_not_back_up_the_previous_version_says_so()
    {
        _fakes.Backups.Fail();
        var editing = await StartedAsync();
        editing.SetDraft("changed");

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Equal("Saved, but the previous version couldn't be backed up.", editing.Note);
    }

    [Fact]
    public async Task Is_saving_while_the_write_is_running()
    {
        var gate = _fakes.EditableFiles.HoldWrites();
        var editing = await StartedAsync();
        editing.SetDraft("changed");

        var saving = editing.SaveAsync();
        var wasSaving = editing.IsSaving;
        gate.SetResult();
        await saving;

        Assert.True(wasSaving);
        Assert.False(editing.IsSaving);
    }

    [Fact]
    public async Task A_file_that_changed_on_disk_is_not_overwritten_and_asks_what_to_do()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "theirs");

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Conflict, outcome);
        Assert.Equal(DiskConflict.Changed, editing.Conflict);
        Assert.Equal("theirs", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Equal("mine", editing.Draft);
        Assert.Null(editing.Error);
    }

    [Fact]
    public async Task A_file_that_was_deleted_on_disk_asks_what_to_do()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.Delete(SkillMd);

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Conflict, outcome);
        Assert.Equal(DiskConflict.Deleted, editing.Conflict);
    }

    [Fact]
    public async Task Cancelling_the_conflict_keeps_the_draft_and_closes_the_question()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "theirs");
        await editing.SaveAsync();

        editing.CancelConflict();

        Assert.Null(editing.Conflict);
        Assert.Equal("mine", editing.Draft);
        Assert.True(editing.IsDirty);
    }

    [Fact]
    public async Task Reloading_takes_the_text_on_disk_and_drops_the_draft()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "theirs\n");
        await editing.SaveAsync();

        await editing.ReloadAsync();

        Assert.Equal("theirs\n", editing.Draft);
        Assert.False(editing.IsDirty);
        Assert.Null(editing.Conflict);
    }

    [Fact]
    public async Task After_a_reload_a_save_checks_against_the_reloaded_file()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "theirs\n");
        await editing.SaveAsync();
        await editing.ReloadAsync();
        editing.SetDraft("merged");

        Assert.Equal(SaveOutcome.Saved, await editing.SaveAsync());
        Assert.Equal("merged", _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Fact]
    public async Task Reloading_a_deleted_file_says_it_cannot_be_read()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.Delete(SkillMd);
        await editing.SaveAsync();

        await editing.ReloadAsync();

        Assert.Null(editing.Conflict);
        Assert.Equal("Couldn't read the file on disk.", editing.Error);
        Assert.Equal("mine", editing.Draft);
    }

    [Fact]
    public async Task Overwriting_saves_over_the_changed_file_and_closes_the_question()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.ChangeBehindTheScenes(SkillMd, "theirs");
        await editing.SaveAsync();

        var outcome = await editing.OverwriteAsync();

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Equal("mine", _fakes.EditableFiles.TextOf(SkillMd));
        Assert.Null(editing.Conflict);
        Assert.False(editing.IsDirty);
    }

    [Fact]
    public async Task Overwriting_writes_a_deleted_file_again()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.Delete(SkillMd);
        await editing.SaveAsync();

        await editing.OverwriteAsync();

        Assert.Equal("mine", _fakes.EditableFiles.TextOf(SkillMd));
    }

    [Theory]
    [InlineData(FileSaveFailure.AccessDenied, "Couldn't save: access to the file was denied.")]
    [InlineData(FileSaveFailure.FolderMissing, "Couldn't save: the folder no longer exists.")]
    [InlineData(FileSaveFailure.Other, "Couldn't save the file.")]
    public async Task A_failed_write_keeps_the_draft_and_says_why(FileSaveFailure failure, string message)
    {
        _fakes.EditableFiles.FailWritesWith(failure);
        var editing = await StartedAsync();
        editing.SetDraft("mine");

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Failed, outcome);
        Assert.Equal(message, editing.Error);
        Assert.Equal("mine", editing.Draft);
        Assert.True(editing.IsDirty);
        Assert.False(editing.IsSaving);
    }

    [Fact]
    public async Task A_file_on_disk_that_cannot_be_read_fails_the_save_with_a_message()
    {
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        _fakes.EditableFiles.MakeUnreadable(SkillMd);

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Failed, outcome);
        Assert.Equal("Couldn't read the file on disk.", editing.Error);
    }

    [Fact]
    public async Task The_next_successful_save_clears_the_error_of_the_one_before()
    {
        _fakes.EditableFiles.FailWritesWith(FileSaveFailure.Other);
        var editing = await StartedAsync();
        editing.SetDraft("mine");
        await editing.SaveAsync();
        _fakes.EditableFiles.FailWritesWith(null);

        var outcome = await editing.SaveAsync();

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Null(editing.Error);
    }

    [Fact]
    public async Task Leaving_without_changes_needs_no_question()
    {
        var editing = await StartedAsync();

        Assert.True(await editing.ConfirmLeaveAsync());
        Assert.False(editing.IsConfirmingDiscard);
    }

    [Fact]
    public async Task Leaving_with_changes_asks_and_discarding_lets_the_user_go()
    {
        var editing = await StartedAsync();
        editing.SetDraft("changed");

        var leaving = editing.ConfirmLeaveAsync();

        Assert.False(leaving.IsCompleted);
        Assert.True(editing.IsConfirmingDiscard);

        editing.Discard();

        Assert.True(await leaving);
        Assert.False(editing.IsConfirmingDiscard);
    }

    [Fact]
    public async Task Leaving_with_changes_and_keeping_editing_stays()
    {
        var editing = await StartedAsync();
        editing.SetDraft("changed");

        var leaving = editing.ConfirmLeaveAsync();
        editing.KeepEditing();

        Assert.False(await leaving);
        Assert.False(editing.IsConfirmingDiscard);
        Assert.Equal("changed", editing.Draft);
    }

    [Fact]
    public async Task Asking_twice_while_the_question_is_open_gives_the_same_answer()
    {
        var editing = await StartedAsync();
        editing.SetDraft("changed");

        var first = editing.ConfirmLeaveAsync();
        var second = editing.ConfirmLeaveAsync();
        editing.Discard();

        Assert.True(await first);
        Assert.True(await second);
    }

    [Fact]
    public async Task Edits_an_instruction_file_too()
    {
        var claudeMd = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, "CLAUDE.md");
        _fakes.InstructionFiles.Add(Project, Agent.ClaudeCode,
            InMemoryInstructionFiles.File("CLAUDE.md", "/work/api", "# Rules\n"));
        var editing = NewEditing(claudeMd);
        await editing.StartAsync();
        editing.SetDraft("# New rules\n");

        await editing.SaveAsync();

        Assert.Equal("# New rules\n", Encoding.UTF8.GetString(_fakes.EditableFiles.BytesOf(claudeMd)!));
    }
}
