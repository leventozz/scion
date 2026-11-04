using System.Text;
using Scion.Application.Editing;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Editing;

public class SaveEditedFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Location Project = new Location.Project("/work/api");
    private static readonly EditableFile SkillMd = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "SKILL.md");
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private readonly List<string> _log = [];
    private readonly InMemoryEditableFiles _files;
    private readonly RecordingBackups _backups;

    public SaveEditedFileTests()
    {
        _files = new InMemoryEditableFiles(_log);
        _backups = new RecordingBackups(_log);
    }

    [Fact]
    public async Task Writes_the_draft_with_the_format_of_the_baseline()
    {
        var baseline = await OpenWith((byte[])[.. Bom, .. Utf8("a\r\nb\r\n")]);

        await SaveAsync("a\nchanged\n", baseline);

        Assert.Equal((byte[])[.. Bom, .. Utf8("a\r\nchanged\r\n")], _files.ContentOf(SkillMd));
    }

    [Fact]
    public async Task Returns_a_baseline_for_what_was_written()
    {
        var baseline = await OpenWith(Utf8("a\r\n"));

        var saved = (await SaveAsync("b\n", baseline)).Value;

        Assert.Equal(ContentHash.Of(_files.ContentOf(SkillMd)), saved.NewBaseline.Hash);
        Assert.Equal(baseline.Format, saved.NewBaseline.Format);
        Assert.False(saved.BackupSkipped);
    }

    [Fact]
    public async Task A_second_save_with_the_new_baseline_succeeds()
    {
        var baseline = await OpenWith(Utf8("a"));
        var first = (await SaveAsync("b", baseline)).Value;

        var second = await SaveAsync("c", first.NewBaseline);

        Assert.True(second.IsSuccess);
        Assert.Equal("c", _files.TextOf(SkillMd));
    }

    [Fact]
    public async Task Backs_up_the_previous_version_before_writing()
    {
        var baseline = await OpenWith(Utf8("a"));
        _log.Clear();

        await SaveAsync("b", baseline);

        Assert.Equal(["backup", "write"], _log);
        Assert.Equal((SkillMd, Now), Assert.Single(_backups.Kept));
    }

    [Fact]
    public async Task A_failed_backup_does_not_stop_the_save_but_is_reported()
    {
        var baseline = await OpenWith(Utf8("a"));
        _backups.Fail();

        var result = await SaveAsync("b", baseline);

        Assert.True(result.Value.BackupSkipped);
        Assert.Equal("b", _files.TextOf(SkillMd));
    }

    [Fact]
    public async Task Refuses_to_save_when_the_file_changed_on_disk_and_touches_nothing()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.Put(SkillMd, "changed elsewhere");
        _log.Clear();

        var result = await SaveAsync("b", baseline);

        Assert.IsType<SaveFileError.ChangedOnDisk>(result.Error);
        Assert.Equal("changed elsewhere", _files.TextOf(SkillMd));
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Refuses_to_save_when_the_file_grew_over_1_MB()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.Put(SkillMd, Enumerable.Repeat((byte)'a', FileText.MaxBytes + 1).ToArray());

        Assert.IsType<SaveFileError.ChangedOnDisk>((await SaveAsync("b", baseline)).Error);
    }

    [Fact]
    public async Task Refuses_to_save_when_the_file_was_deleted()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.Delete(SkillMd);

        var result = await SaveAsync("b", baseline);

        Assert.IsType<SaveFileError.DeletedOnDisk>(result.Error);
        Assert.Equal(0, _files.WriteCalls);
    }

    [Fact]
    public async Task Refuses_to_save_when_the_file_on_disk_cannot_be_read()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.MakeUnreadable(SkillMd);

        Assert.IsType<SaveFileError.Unreadable>((await SaveAsync("b", baseline)).Error);
        Assert.Equal(0, _files.WriteCalls);
    }

    [Fact]
    public async Task Overwrite_saves_over_a_file_that_changed_on_disk_after_backing_it_up()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.Put(SkillMd, "changed elsewhere");
        _log.Clear();

        var result = await SaveAsync("b", baseline, SaveMode.Overwrite);

        Assert.True(result.IsSuccess);
        Assert.Equal(["backup", "write"], _log);
        Assert.Equal("b", _files.TextOf(SkillMd));
    }

    [Fact]
    public async Task Overwrite_writes_a_deleted_file_again_without_a_backup()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.Delete(SkillMd);
        _log.Clear();

        var result = await SaveAsync("b", baseline, SaveMode.Overwrite);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write"], _log);
        Assert.Equal("b", _files.TextOf(SkillMd));
    }

    [Fact]
    public async Task Asks_the_writer_to_verify_the_baseline_just_before_it_replaces_the_file()
    {
        var baseline = await OpenWith(Utf8("a"));

        await SaveAsync("b", baseline);

        Assert.Equal([baseline.Hash], _files.VerifiedHashes);
    }

    [Fact]
    public async Task Overwrite_asks_the_writer_for_no_verification()
    {
        var baseline = await OpenWith(Utf8("a"));

        await SaveAsync("b", baseline, SaveMode.Overwrite);

        Assert.Equal([(string?)null], _files.VerifiedHashes);
    }

    [Fact]
    public async Task A_file_that_changed_while_saving_is_a_conflict_and_is_not_replaced()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.FailWritesWith(FileSaveFailure.ChangedOnDisk);

        var result = await SaveAsync("b", baseline);

        Assert.IsType<SaveFileError.ChangedOnDisk>(result.Error);
        Assert.Equal("a", _files.TextOf(SkillMd));
    }

    [Fact]
    public async Task A_file_that_was_deleted_while_saving_is_a_conflict()
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.FailWritesWith(FileSaveFailure.DeletedOnDisk);

        Assert.IsType<SaveFileError.DeletedOnDisk>((await SaveAsync("b", baseline)).Error);
    }

    [Theory]
    [InlineData(FileSaveFailure.AccessDenied)]
    [InlineData(FileSaveFailure.FolderMissing)]
    [InlineData(FileSaveFailure.Other)]
    public async Task Reports_why_the_write_failed(FileSaveFailure failure)
    {
        var baseline = await OpenWith(Utf8("a"));
        _files.FailWritesWith(failure);

        var result = await SaveAsync("b", baseline);

        Assert.Equal(failure, Assert.IsType<SaveFileError.WriteFailed>(result.Error).Failure);
    }

    [Fact]
    public async Task Rejects_an_unsafe_skill_path_without_touching_anything()
    {
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "../secret.txt");

        var result = await Save(file, "b", new EditBaseline("x", new TextFormat(false, LineEnding.Lf)), SaveMode.CheckForChanges);

        Assert.IsType<SaveFileError.NotInSkillFolder>(result.Error);
        Assert.Equal(0, _files.ReadCalls);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Saves_an_instruction_file()
    {
        var claudeMd = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, ".claude/CLAUDE.md");
        _files.Put(claudeMd, "old");
        var baseline = (await new OpenFileForEditing(_files).ExecuteAsync(claudeMd)).Value.Baseline;

        var result = await Save(claudeMd, "new", baseline, SaveMode.CheckForChanges);

        Assert.True(result.IsSuccess);
        Assert.Equal("new", _files.TextOf(claudeMd));
    }

    private async Task<EditBaseline> OpenWith(byte[] content)
    {
        _files.Put(SkillMd, content);

        return (await new OpenFileForEditing(_files).ExecuteAsync(SkillMd)).Value.Baseline;
    }

    private Task<Result<SavedFile, SaveFileError>> SaveAsync(
        string text, EditBaseline baseline, SaveMode mode = SaveMode.CheckForChanges) =>
        Save(SkillMd, text, baseline, mode);

    private Task<Result<SavedFile, SaveFileError>> Save(
        EditableFile file, string text, EditBaseline baseline, SaveMode mode) =>
        new SaveEditedFile(_files, _files, _backups, new FixedTimeProvider(Now)).ExecuteAsync(file, text, baseline, mode);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
