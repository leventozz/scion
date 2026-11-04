using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemAdaptedSkillWriterTests : IDisposable
{
    private static readonly byte[] ScriptBytes = [0x00, 0xFF, 0x0D, 0x0A, 0x80, 0x7F];
    private static readonly byte[] AdaptedBytes = [0xEF, 0xBB, 0xBF, .. "---\r\nname: docker\r\n---\r\nAdapted\r\n"u8];

    private readonly TempDirectory _sourceProject = new();
    private readonly TempDirectory _targetProject = new();

    public FileSystemAdaptedSkillWriterTests()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Original\n");
        _sourceProject.WriteFile(".claude/skills/docker/references/deep/notes.md", "notes\n");
        Directory.CreateDirectory(Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "scripts"));
        File.WriteAllBytes(Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "scripts", "run.bin"), ScriptBytes);
    }

    public void Dispose()
    {
        _sourceProject.Dispose();
        _targetProject.Dispose();
    }

    private AgentLocation Source => new(new Location.Project(_sourceProject.Path), Agent.ClaudeCode);

    private AgentLocation Target => new(new Location.Project(_targetProject.Path), Agent.ClaudeCode);

    private string TargetSkill => Path.Combine(_targetProject.Path, ".claude", "skills", "docker");

    private string TargetScratch => Path.Combine(_targetProject.Path, ".claude", ".scion-tmp");

    private FileSystemAdaptedSkillWriter Writer() => new(_sourceProject.Path, [new ClaudeCodeLayout(), new CodexLayout()]);

    [Fact]
    public async Task Copy_writes_the_given_bytes_as_SKILL_md_exactly()
    {
        await Writer().CopyAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        Assert.Equal(AdaptedBytes, File.ReadAllBytes(Path.Combine(TargetSkill, "SKILL.md")));
    }

    [Fact]
    public async Task Copy_brings_scripts_and_references_over_byte_for_byte()
    {
        await Writer().CopyAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        Assert.Equal(ScriptBytes, File.ReadAllBytes(Path.Combine(TargetSkill, "scripts", "run.bin")));
        Assert.Equal("notes\n", File.ReadAllText(Path.Combine(TargetSkill, "references", "deep", "notes.md")));
    }

    [Fact]
    public async Task Copy_leaves_the_source_untouched()
    {
        await Writer().CopyAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        Assert.Equal("# Original\n", File.ReadAllText(Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "SKILL.md")));
    }

    [Fact]
    public async Task Copy_fails_and_changes_nothing_when_the_target_already_has_the_skill()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Mine\n");

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Writer().CopyAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None));

        Assert.Equal("# Mine\n", File.ReadAllText(Path.Combine(TargetSkill, "SKILL.md")));
        Assert.Equal(["SKILL.md"], Directory.GetFileSystemEntries(TargetSkill).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Copy_fails_when_the_source_skill_is_missing()
    {
        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Writer().CopyAsync(Source, "missing", Target, AdaptedBytes, CancellationToken.None));
    }

    [Fact]
    public async Task Replace_puts_the_adapted_skill_in_place_of_the_old_folder_with_nothing_of_it_left()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        _targetProject.WriteFile(".claude/skills/docker/only-in-old.txt", "old\n");

        await Writer().ReplaceAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        Assert.Equal(AdaptedBytes, File.ReadAllBytes(Path.Combine(TargetSkill, "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(TargetSkill, "only-in-old.txt")));
        Assert.Equal(ScriptBytes, File.ReadAllBytes(Path.Combine(TargetSkill, "scripts", "run.bin")));
    }

    [Fact]
    public async Task Replace_leaves_no_scratch_directory_behind()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");

        await Writer().ReplaceAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        Assert.False(Directory.Exists(TargetScratch));
    }

    [Fact]
    public async Task Replace_brings_the_old_folder_back_when_the_new_one_cannot_be_moved_in()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        var moves = 0;
        var writer = new FileSystemAdaptedSkillWriter(
            _sourceProject.Path,
            [new ClaudeCodeLayout(), new CodexLayout()],
            (from, to) =>
            {
                if (++moves == 2)
                {
                    throw new IOException("cannot move in");
                }

                Directory.Move(from, to);
            });

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => writer.ReplaceAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None));

        Assert.Equal("# Old\n", File.ReadAllText(Path.Combine(TargetSkill, "SKILL.md")));
    }

    [Fact]
    public async Task Stages_in_the_scratch_directory_beside_the_skills_so_the_start_up_cleanup_finds_leftovers()
    {
        var staged = new List<string>();
        var writer = new FileSystemAdaptedSkillWriter(
            _sourceProject.Path,
            [new ClaudeCodeLayout(), new CodexLayout()],
            (from, to) =>
            {
                staged.Add(from);
                Directory.Move(from, to);
            });

        await writer.CopyAsync(Source, "docker", Target, AdaptedBytes, CancellationToken.None);

        var staging = Assert.Single(staged);
        Assert.Equal(TargetScratch, Path.GetDirectoryName(staging));
        Assert.StartsWith("tmp-docker-", Path.GetFileName(staging));
    }
}
