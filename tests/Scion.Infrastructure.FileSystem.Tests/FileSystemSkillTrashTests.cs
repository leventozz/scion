using System.Text.Json;
using Scion.Application.Deleting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillTrashTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 30, 15, TimeSpan.FromHours(3));
    private static readonly byte[] BinaryBytes = [0x00, 0xFF, 0x0D, 0x0A, 0x80];
    private const string EntryName = "20261103T063015Z-docs";

    private readonly TempDirectory _home = new();
    private readonly TempDirectory _project = new();

    public FileSystemSkillTrashTests()
    {
        _project.WriteFile(".claude/skills/docs/SKILL.md", "---\nname: docs\ndescription: Writes docs.\n---\n# Docs\r\n");
        _project.WriteFile(".claude/skills/docs/scripts/build.sh", "echo build\n");
        File.WriteAllBytes(Path.Combine(SkillFolder, "assets.bin"), BinaryBytes);
        Directory.CreateDirectory(Path.Combine(SkillFolder, "references"));
    }

    public void Dispose()
    {
        _project.Dispose();
        _home.Dispose();
    }

    private string SkillFolder => Path.Combine(_project.Path, ".claude", "skills", "docs");

    private string TrashRoot => Path.Combine(_home.Path, ".scion", "trash");

    private AgentLocation Docs => new(new Location.Project(_project.Path), Agent.ClaudeCode);

    private FileSystemSkillTrash Trash() => new(_home.Path, _home.Path, [new ClaudeCodeLayout(), new CodexLayout()]);

    private FileSystemSkillTrash TrashWith(TrashSteps steps) =>
        new(_home.Path, _home.Path, [new ClaudeCodeLayout(), new CodexLayout()], steps);

    private Task<TrashedSkill> MoveToTrash(FileSystemSkillTrash trash, string folderName = "docs", AgentLocation? place = null) =>
        trash.MoveToTrashAsync(place ?? Docs, folderName, Now, CancellationToken.None);

    private Task<TrashedSkill> MoveToTrash(string folderName = "docs") => MoveToTrash(Trash(), folderName);

    private string[] TrashEntries() =>
        Directory.Exists(TrashRoot)
            ? Directory.GetFileSystemEntries(TrashRoot).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal).ToArray()
            : [];

    private static async Task<SkillTrashFailure> FailureOf(Func<Task> action) =>
        (await Assert.ThrowsAsync<SkillTrashFailedException>(action)).Failure;

    private JsonElement Metadata(string entryName = EntryName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(TrashRoot, entryName + ".json"))).RootElement;

    private void AssertSkillIsIntactInPlace()
    {
        Assert.Equal("echo build\n", File.ReadAllText(Path.Combine(SkillFolder, "scripts", "build.sh")));
        Assert.Equal(BinaryBytes, File.ReadAllBytes(Path.Combine(SkillFolder, "assets.bin")));
        Assert.True(File.Exists(Path.Combine(SkillFolder, "SKILL.md")));
    }

    private void AssertSkillIsInTrash(string entryName = EntryName)
    {
        var entry = Path.Combine(TrashRoot, entryName);
        Assert.Equal("---\nname: docs\ndescription: Writes docs.\n---\n# Docs\r\n", File.ReadAllText(Path.Combine(entry, "SKILL.md")));
        Assert.Equal("echo build\n", File.ReadAllText(Path.Combine(entry, "scripts", "build.sh")));
        Assert.Equal(BinaryBytes, File.ReadAllBytes(Path.Combine(entry, "assets.bin")));
        Assert.True(Directory.Exists(Path.Combine(entry, "references")));
    }

    [Fact]
    public async Task Moves_the_whole_skill_folder_to_the_trash_as_it_is()
    {
        var trashed = await MoveToTrash();

        Assert.True(trashed.OriginalFullyRemoved);
        Assert.False(Directory.Exists(SkillFolder));
        AssertSkillIsInTrash();
        Assert.Equal([EntryName, EntryName + ".json"], TrashEntries());
    }

    [Fact]
    public async Task Names_the_entry_after_the_utc_time_of_the_deletion()
    {
        var evening = new DateTimeOffset(2026, 11, 3, 23, 59, 59, TimeSpan.FromHours(-5));

        await Trash().MoveToTrashAsync(Docs, "docs", evening, CancellationToken.None);

        Assert.Contains("20261104T045959Z-docs", TrashEntries());
    }

    [Fact]
    public async Task Writes_where_the_skill_came_from_next_to_the_entry()
    {
        await MoveToTrash();

        var metadata = Metadata();
        Assert.Equal(SkillFolder, metadata.GetProperty("originalPath").GetString());
        Assert.Equal("project", metadata.GetProperty("location").GetProperty("kind").GetString());
        Assert.Equal(_project.Path, metadata.GetProperty("location").GetProperty("path").GetString());
        Assert.Equal("claudeCode", metadata.GetProperty("agent").GetString());
        Assert.Equal(Now, metadata.GetProperty("deletedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Records_a_personal_skill_without_a_path()
    {
        _home.WriteFile(".agents/skills/docs/SKILL.md", "# Docs\n");
        var personal = new AgentLocation(new Location.Personal(), Agent.Codex);

        await MoveToTrash(Trash(), "docs", personal);

        var metadata = Metadata();
        Assert.Equal(Path.Combine(_home.Path, ".agents", "skills", "docs"), metadata.GetProperty("originalPath").GetString());
        Assert.Equal("personal", metadata.GetProperty("location").GetProperty("kind").GetString());
        Assert.False(metadata.GetProperty("location").TryGetProperty("path", out _));
        Assert.Equal("codex", metadata.GetProperty("agent").GetString());
    }

    [Fact]
    public async Task Never_replaces_an_entry_of_the_same_second()
    {
        await MoveToTrash();
        _project.WriteFile(".claude/skills/docs/SKILL.md", "# Second\n");
        await MoveToTrash();
        _project.WriteFile(".claude/skills/docs/SKILL.md", "# Third\n");
        await MoveToTrash();

        Assert.Equal(
            [EntryName, EntryName + "-2", EntryName + "-2.json", EntryName + "-3", EntryName + "-3.json", EntryName + ".json"],
            TrashEntries());
        Assert.Equal("# Second\n", File.ReadAllText(Path.Combine(TrashRoot, EntryName + "-2", "SKILL.md")));
        Assert.Equal("# Third\n", File.ReadAllText(Path.Combine(TrashRoot, EntryName + "-3", "SKILL.md")));
    }

    [Fact]
    public async Task A_leftover_metadata_file_alone_also_makes_the_name_taken()
    {
        Directory.CreateDirectory(TrashRoot);
        File.WriteAllText(Path.Combine(TrashRoot, EntryName + ".json"), "{}");

        await MoveToTrash();

        Assert.Equal("{}", File.ReadAllText(Path.Combine(TrashRoot, EntryName + ".json")));
        AssertSkillIsInTrash(EntryName + "-2");
    }

    [Fact]
    public async Task A_missing_skill_folder_is_reported_and_nothing_is_written()
    {
        var failure = await FailureOf(() => MoveToTrash("nope"));

        Assert.Equal(SkillTrashFailure.Missing, failure);
        Assert.False(Directory.Exists(Path.Combine(_home.Path, ".scion")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("docs/../docs")]
    [InlineData("..\\docs")]
    public async Task A_name_that_is_not_one_folder_is_refused(string folderName)
    {
        var failure = await FailureOf(() => MoveToTrash(folderName));

        Assert.Equal(SkillTrashFailure.Other, failure);
        AssertSkillIsIntactInPlace();
        Assert.Empty(TrashEntries());
    }

    [Fact]
    public async Task A_failed_move_changes_nothing()
    {
        var trash = TrashWith(TrashSteps.Real with { MoveDirectory = (_, _) => throw new IOException("locked") });

        var failure = await FailureOf(() => MoveToTrash(trash));

        Assert.Equal(SkillTrashFailure.Other, failure);
        AssertSkillIsIntactInPlace();
        Assert.Empty(TrashEntries());
    }

    [Fact]
    public async Task A_linked_skill_folder_is_refused_and_its_target_is_left_alone()
    {
        using var shared = new TempDirectory();
        shared.WriteFile("shared/SKILL.md", "# Shared\n");
        _project.LinkDirectory(".claude/skills/shared", Path.Combine(shared.Path, "shared"));

        var failure = await FailureOf(() => MoveToTrash("shared"));

        Assert.Equal(SkillTrashFailure.Linked, failure);
        Assert.Equal("# Shared\n", File.ReadAllText(Path.Combine(shared.Path, "shared", "SKILL.md")));
        Assert.True(Directory.Exists(Path.Combine(_project.Path, ".claude", "skills", "shared")));
        Assert.False(Directory.Exists(Path.Combine(_home.Path, ".scion")));
    }

    [Fact]
    public async Task A_skill_in_a_linked_skills_directory_is_refused()
    {
        using var shared = new TempDirectory();
        using var other = new TempDirectory();
        shared.WriteFile("skills/team/SKILL.md", "# Team\n");
        other.LinkDirectory(".claude/skills", Path.Combine(shared.Path, "skills"));
        var place = new AgentLocation(new Location.Project(other.Path), Agent.ClaudeCode);

        var failure = await FailureOf(() => MoveToTrash(Trash(), "team", place));

        Assert.Equal(SkillTrashFailure.Linked, failure);
        Assert.Equal("# Team\n", File.ReadAllText(Path.Combine(shared.Path, "skills", "team", "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(_home.Path, ".scion")));
    }

    [Fact]
    public async Task A_skill_in_a_linked_agent_folder_is_refused()
    {
        using var shared = new TempDirectory();
        using var other = new TempDirectory();
        shared.WriteFile("agent/skills/team/SKILL.md", "# Team\n");
        other.LinkDirectory(".claude", Path.Combine(shared.Path, "agent"));
        var place = new AgentLocation(new Location.Project(other.Path), Agent.ClaudeCode);

        var failure = await FailureOf(() => MoveToTrash(Trash(), "team", place));

        Assert.Equal(SkillTrashFailure.Linked, failure);
        Assert.Equal("# Team\n", File.ReadAllText(Path.Combine(shared.Path, "agent", "skills", "team", "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(_home.Path, ".scion")));
    }

    /// <summary>A move across volumes cannot be made with real files in a test, so the move step says it happened.</summary>
    public class AcrossVolumes : IDisposable
    {
        private readonly FileSystemSkillTrashTests _outer = new();

        public void Dispose() => _outer.Dispose();

        private TrashSteps SourceIsOnAnotherVolume => TrashSteps.Real with
        {
            MoveDirectory = (from, to) =>
            {
                if (from == _outer.SkillFolder)
                {
                    throw new DirectoryMoveAcrossVolumesException();
                }

                Directory.Move(from, to);
            },
        };

        private string[] Staging() =>
            Directory.Exists(_outer.TrashRoot)
                ? Directory.GetDirectories(_outer.TrashRoot, ".incoming-*")
                : [];

        [Fact]
        public async Task Copies_the_skill_to_the_trash_and_removes_the_original()
        {
            var trashed = await _outer.MoveToTrash(_outer.TrashWith(SourceIsOnAnotherVolume));

            Assert.True(trashed.OriginalFullyRemoved);
            Assert.False(Directory.Exists(_outer.SkillFolder));
            _outer.AssertSkillIsInTrash();
            Assert.Equal([EntryName, EntryName + ".json"], _outer.TrashEntries());
        }

        [Fact]
        public async Task Removes_read_only_files_of_the_original_too()
        {
            File.SetAttributes(Path.Combine(_outer.SkillFolder, "scripts", "build.sh"), FileAttributes.ReadOnly);

            var trashed = await _outer.MoveToTrash(_outer.TrashWith(SourceIsOnAnotherVolume));

            // The copy in the trash keeps the attribute; clear it so the test can clean up.
            File.SetAttributes(Path.Combine(_outer.TrashRoot, EntryName, "scripts", "build.sh"), FileAttributes.Normal);
            Assert.True(trashed.OriginalFullyRemoved);
            Assert.False(Directory.Exists(_outer.SkillFolder));
        }

        [Fact]
        public async Task A_failed_copy_leaves_the_original_alone_and_no_staging_or_metadata()
        {
            var steps = SourceIsOnAnotherVolume with
            {
                CopyTree = (_, staging) =>
                {
                    Directory.CreateDirectory(staging);
                    File.WriteAllText(Path.Combine(staging, "SKILL.md"), "half");
                    throw new IOException("disk full");
                },
            };

            var failure = await FailureOf(() => _outer.MoveToTrash(_outer.TrashWith(steps)));

            Assert.Equal(SkillTrashFailure.Other, failure);
            _outer.AssertSkillIsIntactInPlace();
            Assert.Empty(_outer.TrashEntries());
        }

        [Fact]
        public async Task A_failed_move_out_of_the_staging_folder_leaves_the_original_alone_and_cleans_up()
        {
            var steps = SourceIsOnAnotherVolume with
            {
                MoveDirectory = (from, _) => throw (from == _outer.SkillFolder
                    ? new DirectoryMoveAcrossVolumesException()
                    : new IOException("locked")),
            };

            var failure = await FailureOf(() => _outer.MoveToTrash(_outer.TrashWith(steps)));

            Assert.Equal(SkillTrashFailure.Other, failure);
            _outer.AssertSkillIsIntactInPlace();
            Assert.Empty(_outer.TrashEntries());
        }

        [Fact]
        public async Task A_removal_that_stops_halfway_is_reported_and_the_trash_copy_stays()
        {
            var steps = SourceIsOnAnotherVolume with { DeleteTree = _ => throw new IOException("locked") };

            var trashed = await _outer.MoveToTrash(_outer.TrashWith(steps));

            Assert.False(trashed.OriginalFullyRemoved);
            Assert.True(Directory.Exists(_outer.SkillFolder));
            _outer.AssertSkillIsInTrash();
            Assert.True(File.Exists(Path.Combine(_outer.TrashRoot, EntryName + ".json")));
            Assert.Empty(Staging());
        }

        [Fact]
        public async Task A_link_inside_the_skill_is_removed_without_touching_its_target()
        {
            using var shared = new TempDirectory();
            shared.WriteFile("notes/keep.txt", "keep\n");
            Directory.Delete(Path.Combine(_outer.SkillFolder, "references"));
            _outer._project.LinkDirectory(".claude/skills/docs/references", Path.Combine(shared.Path, "notes"));

            var trashed = await _outer.MoveToTrash(_outer.TrashWith(SourceIsOnAnotherVolume));

            Assert.True(trashed.OriginalFullyRemoved);
            Assert.False(Directory.Exists(_outer.SkillFolder));
            Assert.Equal("keep\n", File.ReadAllText(Path.Combine(shared.Path, "notes", "keep.txt")));
        }
    }
}
