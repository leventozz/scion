using System.Text.RegularExpressions;
using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemFileBackupsTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = [0xEF, 0xBB, 0xBF, 0x61, 0x0D, 0x0A, 0xFF, 0x00];

    private readonly TempDirectory _scionHome = new();
    private readonly TempDirectory _project = new();

    public void Dispose()
    {
        _scionHome.Dispose();
        _project.Dispose();
    }

    private Location Project => new Location.Project(_project.Path);

    private EditableFile.SkillFile SkillMd => new(Project, Agent.ClaudeCode, "pdf", "SKILL.md");

    private string SkillMdPath => Path.Combine(_project.Path, ".claude", "skills", "pdf", "SKILL.md");

    private string BackupsRoot => Path.Combine(_scionHome.Path, ".scion", "backups");

    private FileSystemFileBackups BackupsWith(StringComparison comparison = StringComparison.Ordinal) =>
        new(_scionHome.Path, _project.Path, [new ClaudeCodeLayout(), new CodexLayout()], [new ClaudeCodeLayout(), new CodexLayout()], comparison);

    private void WriteSkillMd(byte[]? content = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SkillMdPath)!);
        File.WriteAllBytes(SkillMdPath, content ?? Bytes);
    }

    private string OnlyBackupFolder() => Assert.Single(Directory.GetDirectories(BackupsRoot));

    private static List<string> BackupNamesIn(string folder) =>
        Directory.GetFiles(folder).Select(Path.GetFileName).Where(name => name != "source.txt").Order(StringComparer.Ordinal).ToList()!;

    [Fact]
    public async Task Copies_the_file_as_it_is_on_disk_under_a_UTC_timestamp()
    {
        WriteSkillMd();

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        var backup = Path.Combine(OnlyBackupFolder(), "20261101T120000000Z-SKILL.md");
        Assert.Equal(Bytes, File.ReadAllBytes(backup));
    }

    [Fact]
    public async Task Converts_a_local_time_to_UTC_for_the_name()
    {
        WriteSkillMd();

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, new DateTimeOffset(2026, 11, 1, 15, 30, 5, 7, TimeSpan.FromHours(3)), CancellationToken.None);

        Assert.Equal(["20261101T123005007Z-SKILL.md"], BackupNamesIn(OnlyBackupFolder()));
    }

    [Fact]
    public async Task Keeps_the_backups_of_a_file_in_a_folder_named_after_it_and_a_hash_of_its_path()
    {
        WriteSkillMd();

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        var folder = OnlyBackupFolder();
        Assert.Equal(BackupsRoot, Path.GetDirectoryName(folder));
        Assert.Matches(new Regex(@"^SKILL\.md-[0-9a-f]{16}$"), Path.GetFileName(folder));
    }

    [Fact]
    public async Task A_source_file_records_the_original_path_so_a_backup_can_be_restored_by_hand()
    {
        WriteSkillMd();

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        Assert.Equal(SkillMdPath, File.ReadAllText(Path.Combine(OnlyBackupFolder(), "source.txt")));
    }

    [Fact]
    public async Task Backups_of_the_same_file_share_a_folder()
    {
        WriteSkillMd();
        var backups = BackupsWith();

        await backups.KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);
        await backups.KeepPreviousVersionAsync(SkillMd, Noon.AddMinutes(1), CancellationToken.None);

        Assert.Equal(
            ["20261101T120000000Z-SKILL.md", "20261101T120100000Z-SKILL.md"],
            BackupNamesIn(OnlyBackupFolder()));
    }

    [Fact]
    public async Task Files_with_the_same_name_in_different_places_get_different_folders()
    {
        WriteSkillMd();
        _project.WriteFile(".claude/skills/other/SKILL.md", "other");
        var backups = BackupsWith();

        await backups.KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);
        await backups.KeepPreviousVersionAsync(
            new EditableFile.SkillFile(Project, Agent.ClaudeCode, "other", "SKILL.md"), Noon, CancellationToken.None);

        Assert.Equal(2, Directory.GetDirectories(BackupsRoot).Length);
    }

    [Fact]
    public async Task Backs_up_an_instruction_file()
    {
        _project.WriteFile("CLAUDE.md", "# Rules");

        await BackupsWith().KeepPreviousVersionAsync(
            new EditableFile.InstructionFile(Project, Agent.ClaudeCode, "CLAUDE.md"), Noon, CancellationToken.None);

        var folder = OnlyBackupFolder();
        Assert.StartsWith("CLAUDE.md-", Path.GetFileName(folder));
        Assert.Equal("# Rules", File.ReadAllText(Path.Combine(folder, "20261101T120000000Z-CLAUDE.md")));
    }

    [Fact]
    public async Task A_clash_in_the_same_millisecond_gets_a_number()
    {
        WriteSkillMd();
        var backups = BackupsWith();

        for (var save = 0; save < 3; save++)
        {
            await backups.KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);
        }

        Assert.Equal(
            ["20261101T120000000Z-SKILL.md", "20261101T120000000Z-SKILL.md-2", "20261101T120000000Z-SKILL.md-3"],
            BackupNamesIn(OnlyBackupFolder()));
    }

    [Fact]
    public async Task A_file_that_does_not_exist_is_a_no_op()
    {
        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        Assert.False(Directory.Exists(BackupsRoot));
    }

    [Fact]
    public async Task Keeps_only_the_last_twenty_backups()
    {
        WriteSkillMd();
        var backups = BackupsWith();

        for (var save = 0; save < 21; save++)
        {
            await backups.KeepPreviousVersionAsync(SkillMd, Noon.AddMinutes(save), CancellationToken.None);
        }

        var names = BackupNamesIn(OnlyBackupFolder());
        Assert.Equal(20, names.Count);
        Assert.DoesNotContain("20261101T120000000Z-SKILL.md", names);
        Assert.Contains("20261101T122000000Z-SKILL.md", names);
        Assert.True(File.Exists(Path.Combine(OnlyBackupFolder(), "source.txt")));
    }

    [Fact]
    public async Task Leaves_no_temp_file_behind()
    {
        WriteSkillMd();

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        Assert.DoesNotContain(Directory.GetFiles(OnlyBackupFolder()), file => file.EndsWith(".tmp"));
    }

    [Fact]
    public async Task Backs_up_a_file_another_process_holds_open_for_writing()
    {
        WriteSkillMd();
        using var writer = new FileStream(SkillMdPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        await BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None);

        Assert.Equal(Bytes, File.ReadAllBytes(Path.Combine(OnlyBackupFolder(), "20261101T120000000Z-SKILL.md")));
    }

    [Fact]
    public async Task Fails_with_a_backup_exception_when_the_backup_folder_cannot_be_made()
    {
        WriteSkillMd();
        File.WriteAllText(Path.Combine(_scionHome.Path, ".scion"), "not a folder");

        await Assert.ThrowsAsync<FileBackupFailedException>(
            () => BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None));
    }

    [Fact]
    public async Task Fails_with_a_backup_exception_when_the_file_is_locked_exclusively()
    {
        WriteSkillMd();
        using var locked = new FileStream(SkillMdPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<FileBackupFailedException>(
            () => BackupsWith().KeepPreviousVersionAsync(SkillMd, Noon, CancellationToken.None));
    }

    [Fact]
    public void A_case_insensitive_file_system_names_a_file_in_any_case_the_same_way()
    {
        var upper = FileSystemFileBackups.FolderNameOf("/Work/API/SKILL.md", StringComparison.OrdinalIgnoreCase);
        var lower = FileSystemFileBackups.FolderNameOf("/work/api/SKILL.md", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(upper, lower);
    }

    [Fact]
    public void A_case_sensitive_file_system_names_files_that_differ_in_case_differently()
    {
        var upper = FileSystemFileBackups.FolderNameOf("/work/API/SKILL.md", StringComparison.Ordinal);
        var lower = FileSystemFileBackups.FolderNameOf("/work/api/SKILL.md", StringComparison.Ordinal);

        Assert.NotEqual(upper, lower);
    }
}
