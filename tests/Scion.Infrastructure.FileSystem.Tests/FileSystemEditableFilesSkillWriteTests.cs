using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

/// <summary>
/// The file move is the one step that cannot be made to fail with real files, so these tests inject it.
/// Everything else runs against the real file system.
/// </summary>
public class FileSystemEditableFilesSkillWriteTests : IDisposable
{
    private static readonly byte[] OldBytes = [0xEF, 0xBB, 0xBF, 0x61, 0x0D, 0x0A, 0x62];
    private static readonly byte[] NewBytes = [0xEF, 0xBB, 0xBF, 0x63, 0x0D, 0x0A, 0xC3, 0xA7];

    private readonly TempDirectory _project = new();

    public void Dispose() => _project.Dispose();

    private Location Project => new Location.Project(_project.Path);

    private EditableFile.SkillFile SkillMd => new(Project, Agent.ClaudeCode, "pdf", "SKILL.md");

    private string SkillFolder => Path.Combine(_project.Path, ".claude", "skills", "pdf");

    private string TargetPath => Path.Combine(SkillFolder, "SKILL.md");

    private string Scratch => Path.Combine(_project.Path, ".claude", ".scion-tmp");

    private FileSystemEditableFiles FilesWith(Moves moves) =>
        new(_project.Path, [new ClaudeCodeLayout(), new CodexLayout()], [new ClaudeCodeLayout(), new CodexLayout()], moves.Move);

    private Task WriteAsync(EditableFile file, byte[] content, Moves? moves = null, string? unchangedSinceHash = null) =>
        FilesWith(moves ?? new Moves()).WriteAsync(file, content, unchangedSinceHash, CancellationToken.None);

    private void WriteOldSkill()
    {
        Directory.CreateDirectory(SkillFolder);
        File.WriteAllBytes(TargetPath, OldBytes);
    }

    [Fact]
    public async Task Replaces_the_content_byte_for_byte()
    {
        WriteOldSkill();

        await WriteAsync(SkillMd, NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(TargetPath));
    }

    [Fact]
    public async Task Replaces_the_file_when_it_still_has_the_content_the_edit_started_from()
    {
        WriteOldSkill();

        await WriteAsync(SkillMd, NewBytes, unchangedSinceHash: ContentHash.Of(OldBytes));

        Assert.Equal(NewBytes, File.ReadAllBytes(TargetPath));
    }

    [Fact]
    public async Task Does_not_replace_a_file_that_changed_since_the_edit_started_and_leaves_no_scratch()
    {
        WriteOldSkill();
        var hashWhenOpened = ContentHash.Of(OldBytes);
        File.WriteAllText(TargetPath, "changed elsewhere");
        var moves = new Moves();

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(
            () => WriteAsync(SkillMd, NewBytes, moves, hashWhenOpened));

        Assert.Equal(FileSaveFailure.ChangedOnDisk, failure.Failure);
        Assert.Equal("changed elsewhere", File.ReadAllText(TargetPath));
        Assert.Empty(moves.Done);
        Assert.False(Directory.Exists(Scratch));
    }

    [Fact]
    public async Task Does_not_write_a_file_that_was_deleted_since_the_edit_started()
    {
        Directory.CreateDirectory(SkillFolder);

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(
            () => WriteAsync(SkillMd, NewBytes, unchangedSinceHash: ContentHash.Of(OldBytes)));

        Assert.Equal(FileSaveFailure.DeletedOnDisk, failure.Failure);
        Assert.False(File.Exists(TargetPath));
    }

    [Fact]
    public async Task Replaces_a_changed_file_when_no_check_is_asked_for()
    {
        WriteOldSkill();
        File.WriteAllText(TargetPath, "changed elsewhere");

        await WriteAsync(SkillMd, NewBytes, unchangedSinceHash: null);

        Assert.Equal(NewBytes, File.ReadAllBytes(TargetPath));
    }

    [Fact]
    public async Task Writes_a_file_that_does_not_exist_yet_in_an_existing_skill_folder()
    {
        Directory.CreateDirectory(SkillFolder);

        await WriteAsync(SkillMd, NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(TargetPath));
    }

    [Fact]
    public async Task Writes_a_file_in_a_sub_folder_of_the_skill()
    {
        _project.WriteFile(".claude/skills/pdf/scripts/run.sh", "old");
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "scripts/run.sh");

        await WriteAsync(file, NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(Path.Combine(SkillFolder, "scripts", "run.sh")));
    }

    [Fact]
    public async Task Stages_in_the_scratch_directory_of_the_agent_folder_and_not_in_the_skills_directory()
    {
        WriteOldSkill();
        var moves = new Moves();

        await WriteAsync(SkillMd, NewBytes, moves);

        var move = Assert.Single(moves.Done);
        Assert.Equal(Scratch, Path.GetDirectoryName(move.From));
        Assert.StartsWith("edit-", Path.GetFileName(move.From));
        Assert.Equal(TargetPath, move.To);
        Assert.Equal(["SKILL.md"], Directory.GetFileSystemEntries(SkillFolder).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Stages_a_codex_skill_in_the_scratch_directory_of_the_agents_folder()
    {
        _project.WriteFile(".agents/skills/pdf/SKILL.md", "old");
        var moves = new Moves();

        await WriteAsync(new EditableFile.SkillFile(Project, Agent.Codex, "pdf", "SKILL.md"), NewBytes, moves);

        Assert.Equal(
            Path.Combine(_project.Path, ".agents", ".scion-tmp"),
            Path.GetDirectoryName(Assert.Single(moves.Done).From));
    }

    [Fact]
    public async Task Leaves_no_scratch_directory_behind()
    {
        WriteOldSkill();

        await WriteAsync(SkillMd, NewBytes);

        Assert.False(Directory.Exists(Scratch));
    }

    [Fact]
    public async Task A_failed_move_keeps_the_file_intact_and_leaves_no_temp_file()
    {
        WriteOldSkill();
        var moves = new Moves(failWith: () => new IOException("Simulated failure of a file move."));

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(SkillMd, NewBytes, moves));

        Assert.Equal(FileSaveFailure.Other, failure.Failure);
        Assert.Equal(OldBytes, File.ReadAllBytes(TargetPath));
        Assert.False(Directory.Exists(Scratch));
    }

    [Fact]
    public async Task A_move_that_is_denied_is_an_access_denied_failure()
    {
        WriteOldSkill();
        var moves = new Moves(failWith: () => new UnauthorizedAccessException());

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(SkillMd, NewBytes, moves));

        Assert.Equal(FileSaveFailure.AccessDenied, failure.Failure);
        Assert.Equal(OldBytes, File.ReadAllBytes(TargetPath));
    }

    [Fact]
    public async Task A_move_into_a_folder_that_vanished_is_a_folder_missing_failure()
    {
        WriteOldSkill();
        var moves = new Moves(failWith: () => new DirectoryNotFoundException());

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(SkillMd, NewBytes, moves));

        Assert.Equal(FileSaveFailure.FolderMissing, failure.Failure);
    }

    [Fact]
    public async Task A_read_only_file_is_not_written()
    {
        WriteOldSkill();
        File.SetAttributes(TargetPath, FileAttributes.ReadOnly);

        try
        {
            var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(SkillMd, NewBytes));

            Assert.Equal(FileSaveFailure.AccessDenied, failure.Failure);
            Assert.Equal(OldBytes, File.ReadAllBytes(TargetPath));
            Assert.False(Directory.Exists(Scratch));
        }
        finally
        {
            File.SetAttributes(TargetPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task A_missing_skill_folder_is_a_folder_missing_failure_and_nothing_is_created()
    {
        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(SkillMd, NewBytes));

        Assert.Equal(FileSaveFailure.FolderMissing, failure.Failure);
        Assert.Empty(Directory.GetFileSystemEntries(_project.Path));
    }

    [Fact]
    public async Task A_missing_sub_folder_is_a_folder_missing_failure()
    {
        WriteOldSkill();
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, "pdf", "scripts/run.sh");

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(file, NewBytes));

        Assert.Equal(FileSaveFailure.FolderMissing, failure.Failure);
        Assert.False(Directory.Exists(Path.Combine(SkillFolder, "scripts")));
    }

    [Theory]
    [InlineData("..", "SKILL.md")]
    [InlineData("pdf/other", "SKILL.md")]
    [InlineData("pdf", "../other/SKILL.md")]
    [InlineData("pdf", "scripts/../../other/SKILL.md")]
    public async Task Refuses_a_path_that_leaves_the_skill_folder(string folderName, string relativePath)
    {
        WriteOldSkill();
        var file = new EditableFile.SkillFile(Project, Agent.ClaudeCode, folderName, relativePath);

        await Assert.ThrowsAsync<ArgumentException>(() => WriteAsync(file, NewBytes));

        Assert.Equal(OldBytes, File.ReadAllBytes(TargetPath));
    }

    /// <summary>Moves files for real, except where the test says a move fails.</summary>
    private sealed class Moves(Func<Exception>? failWith = null)
    {
        public List<(string From, string To)> Done { get; } = [];

        public void Move(string from, string to)
        {
            if (failWith is not null)
            {
                throw failWith();
            }

            File.Move(from, to, overwrite: true);
            Done.Add((from, to));
        }
    }
}
