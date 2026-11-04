using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

/// <summary>
/// The directory move is the one step that cannot be made to fail with real files, so these tests inject it.
/// Everything else in the copier runs against the real file system.
/// </summary>
public class FileSystemSkillCopierMoveFailureTests : IDisposable
{
    private static readonly byte[] OldBytes = [0x00, 0xFF, 0x0D, 0x0A, 0x80];

    private readonly TempDirectory _sourceProject = new();
    private readonly TempDirectory _targetProject = new();

    public FileSystemSkillCopierMoveFailureTests()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# New\n");
        _sourceProject.WriteFile(".claude/skills/docker/scripts/new.sh", "echo new\n");
    }

    public void Dispose()
    {
        _sourceProject.Dispose();
        _targetProject.Dispose();
    }

    private string TargetSkills => Path.Combine(_targetProject.Path, ".claude", "skills");

    private string TargetSkill => Path.Combine(TargetSkills, "docker");

    private string TargetScratch => Path.Combine(_targetProject.Path, ".claude", ".scion-tmp");

    private AgentLocation Source => new(new Location.Project(_sourceProject.Path), Agent.ClaudeCode);

    private AgentLocation Target => new(new Location.Project(_targetProject.Path), Agent.ClaudeCode);

    private FileSystemSkillCopier CopierWith(Moves moves) =>
        new(_sourceProject.Path, [new ClaudeCodeLayout(), new CodexLayout()], moves.Move);

    private void WriteOldTarget()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        File.WriteAllBytes(Path.Combine(TargetSkill, "old.bin"), OldBytes);
    }

    private void AssertOldTargetIsIntact()
    {
        Assert.Equal("# Old\n", File.ReadAllText(Path.Combine(TargetSkill, "SKILL.md")));
        Assert.Equal(OldBytes, File.ReadAllBytes(Path.Combine(TargetSkill, "old.bin")));
        Assert.Equal(["SKILL.md", "old.bin"], Directory.GetFiles(TargetSkill).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.GetDirectories(TargetSkill));
    }

    [Fact]
    public async Task Stages_in_the_scratch_directory_of_the_agent_folder_and_not_in_the_skills_directory()
    {
        var moves = new Moves();

        await CopierWith(moves).CopyAsync(Source, "docker", Target, CancellationToken.None);

        var staging = Assert.Single(moves.Done).From;
        Assert.Equal(TargetScratch, Path.GetDirectoryName(staging));
        Assert.Equal(["docker"], Directory.GetDirectories(TargetSkills).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Leaves_no_scratch_directory_behind_after_a_copy_and_after_a_replace()
    {
        WriteOldTarget();
        var copier = CopierWith(new Moves());

        await copier.ReplaceAsync(Source, "docker", Target, CancellationToken.None);
        Assert.False(Directory.Exists(TargetScratch));

        Directory.Delete(TargetSkill, recursive: true);
        await copier.CopyAsync(Source, "docker", Target, CancellationToken.None);
        Assert.False(Directory.Exists(TargetScratch));
    }

    [Fact]
    public async Task Copy_leaves_nothing_behind_when_the_final_move_fails()
    {
        var moves = new Moves(failWhen: (_, to) => to == TargetSkill);

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => CopierWith(moves).CopyAsync(Source, "docker", Target, CancellationToken.None));

        Assert.False(Directory.Exists(TargetSkill));
        Assert.Empty(Directory.GetFileSystemEntries(TargetSkills));
        Assert.False(Directory.Exists(TargetScratch));
    }

    [Fact]
    public async Task Replace_puts_the_old_folder_back_with_its_original_bytes_when_moving_the_new_one_in_fails()
    {
        WriteOldTarget();
        var moves = new Moves(failWhen: (from, _) => Path.GetFileName(from).StartsWith("tmp-"));

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => CopierWith(moves).ReplaceAsync(Source, "docker", Target, CancellationToken.None));

        AssertOldTargetIsIntact();
        Assert.Equal(["docker"], Directory.GetDirectories(TargetSkills).Select(Path.GetFileName));
        Assert.False(Directory.Exists(TargetScratch));
    }

    [Fact]
    public async Task Replace_leaves_the_old_folder_untouched_when_setting_it_aside_fails()
    {
        WriteOldTarget();
        var moves = new Moves(failWhen: (from, _) => from == TargetSkill);

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => CopierWith(moves).ReplaceAsync(Source, "docker", Target, CancellationToken.None));

        AssertOldTargetIsIntact();
        Assert.False(Directory.Exists(TargetScratch));
    }

    [Fact]
    public async Task Replace_keeps_the_old_folder_in_the_scratch_directory_when_even_putting_it_back_fails()
    {
        WriteOldTarget();
        var moves = new Moves(failWhen: (_, to) => to == TargetSkill);

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => CopierWith(moves).ReplaceAsync(Source, "docker", Target, CancellationToken.None));

        var kept = Assert.Single(Directory.GetDirectories(TargetScratch));
        Assert.StartsWith("old-docker-", Path.GetFileName(kept));
        Assert.Equal("# Old\n", File.ReadAllText(Path.Combine(kept, "SKILL.md")));
        Assert.Equal(OldBytes, File.ReadAllBytes(Path.Combine(kept, "old.bin")));
    }

    /// <summary>Moves directories for real, except where the test says a move fails.</summary>
    private sealed class Moves(Func<string, string, bool>? failWhen = null)
    {
        public List<(string From, string To)> Done { get; } = [];

        public void Move(string from, string to)
        {
            if (failWhen?.Invoke(from, to) == true)
            {
                throw new IOException("Simulated failure of a directory move.");
            }

            Directory.Move(from, to);
            Done.Add((from, to));
        }
    }
}
