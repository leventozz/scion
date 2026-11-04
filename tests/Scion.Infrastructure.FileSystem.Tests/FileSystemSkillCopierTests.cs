using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillCopierTests : IDisposable
{
    private readonly TempDirectory _sourceProject = new();
    private readonly TempDirectory _targetProject = new();

    public void Dispose()
    {
        _sourceProject.Dispose();
        _targetProject.Dispose();
    }

    private AgentLocation Source(Agent agent = Agent.ClaudeCode) => new(new Location.Project(_sourceProject.Path), agent);

    private AgentLocation Target(Agent agent = Agent.ClaudeCode) => new(new Location.Project(_targetProject.Path), agent);

    private static FileSystemSkillCopier CopierWithHome(string homeDirectory) =>
        new(homeDirectory, [new ClaudeCodeLayout(), new CodexLayout()]);

    private FileSystemSkillCopier Copier() => CopierWithHome(_sourceProject.Path);

    private string TargetFile(string relativePath) => Path.Combine(_targetProject.Path, relativePath);

    private static string[] FolderNamesIn(string skillsDirectory) =>
        Directory.GetDirectories(skillsDirectory).Select(directory => Path.GetFileName(directory)!).Order().ToArray();

    [Fact]
    public async Task Lists_every_sub_folder_of_the_skills_directory_with_or_without_a_SKILL_md()
    {
        _targetProject.WriteFile(".claude/skills/real-skill/SKILL.md", "# Real\n");
        _targetProject.WriteFile(".claude/skills/only-notes/notes.md", "# Not a skill\n");
        _targetProject.WriteFile(".claude/skills/loose-file.md", "# Not a folder\n");

        var names = await Copier().ListAsync(Target(), CancellationToken.None);

        Assert.Equal(["only-notes", "real-skill"], names.Order());
    }

    [Fact]
    public async Task Lists_nothing_when_the_skills_directory_does_not_exist()
    {
        var names = await Copier().ListAsync(Target(), CancellationToken.None);

        Assert.Empty(names);
    }

    [Fact]
    public async Task Lists_the_skills_directory_of_the_given_agent()
    {
        _targetProject.WriteFile(".claude/skills/for-claude/SKILL.md", "#\n");
        _targetProject.WriteFile(".agents/skills/for-codex/SKILL.md", "#\n");

        var names = await Copier().ListAsync(Target(Agent.Codex), CancellationToken.None);

        Assert.Equal(["for-codex"], names);
    }

    [Fact]
    public async Task Lists_the_personal_skills_under_the_injected_home_directory()
    {
        _sourceProject.WriteFile(".claude/skills/personal-skill/SKILL.md", "#\n");
        var personal = new AgentLocation(new Location.Personal(), Agent.ClaudeCode);

        var names = await CopierWithHome(_sourceProject.Path).ListAsync(personal, CancellationToken.None);

        Assert.Equal(["personal-skill"], names);
    }

    [Fact]
    public async Task Copies_the_whole_folder_tree_including_sub_folders()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _sourceProject.WriteFile(".claude/skills/docker/scripts/run.sh", "echo run\n");
        _sourceProject.WriteFile(".claude/skills/docker/references/deep/notes.md", "notes\n");
        _sourceProject.WriteFile(".claude/skills/other/SKILL.md", "# Other\n");

        await Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None);

        Assert.Equal("# Docker\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
        Assert.Equal("echo run\n", File.ReadAllText(TargetFile(".claude/skills/docker/scripts/run.sh")));
        Assert.Equal("notes\n", File.ReadAllText(TargetFile(".claude/skills/docker/references/deep/notes.md")));
        Assert.Equal(["docker"], FolderNamesIn(TargetFile(".claude/skills")));
    }

    [Fact]
    public async Task Keeps_binary_files_byte_for_byte()
    {
        byte[] bytes = [0x00, 0xFF, 0x0D, 0x0A, 0x80, 0x1A, 0x00];
        var sourceFile = Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "assets", "logo.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        await File.WriteAllBytesAsync(sourceFile, bytes);

        await Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(TargetFile(".claude/skills/docker/assets/logo.bin")));
    }

    [Fact]
    public async Task Creates_the_target_skills_directory_when_it_is_missing()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");

        await Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None);

        Assert.True(File.Exists(TargetFile(".claude/skills/docker/SKILL.md")));
    }

    [Fact]
    public async Task Copies_from_the_claude_layout_to_the_codex_layout()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");

        await Copier().CopyAsync(Source(Agent.ClaudeCode), "docker", Target(Agent.Codex), CancellationToken.None);

        Assert.True(File.Exists(TargetFile(".agents/skills/docker/SKILL.md")));
        Assert.False(Directory.Exists(TargetFile(".claude")));
    }

    [Fact]
    public async Task Copies_from_the_codex_layout_to_the_claude_layout()
    {
        _sourceProject.WriteFile(".agents/skills/docker/SKILL.md", "# Docker\n");

        await Copier().CopyAsync(Source(Agent.Codex), "docker", Target(Agent.ClaudeCode), CancellationToken.None);

        Assert.True(File.Exists(TargetFile(".claude/skills/docker/SKILL.md")));
    }

    [Fact]
    public async Task Copies_from_the_personal_skills_into_a_project()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Personal docker\n");
        var personal = new AgentLocation(new Location.Personal(), Agent.ClaudeCode);

        await CopierWithHome(_sourceProject.Path).CopyAsync(personal, "docker", Target(), CancellationToken.None);

        Assert.Equal("# Personal docker\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
    }

    [Fact]
    public async Task Copy_refuses_a_target_that_already_has_the_folder_and_does_not_touch_it()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# New\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None));

        Assert.Equal("# Old\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
        Assert.Equal(["docker"], FolderNamesIn(TargetFile(".claude/skills")));
    }

    [Fact]
    public async Task Copy_fails_without_leaving_a_folder_behind_when_the_source_is_missing()
    {
        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None));

        Assert.False(Directory.Exists(TargetFile(".claude/skills/docker")));
    }

    [Fact]
    public async Task Copy_leaves_no_half_copy_when_a_file_cannot_be_read()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _sourceProject.WriteFile(".claude/skills/docker/scripts/locked.sh", "echo locked\n");
        using var heldOpen = new FileStream(
            Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "scripts", "locked.sh"),
            FileMode.Open, FileAccess.Read, FileShare.None);

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().CopyAsync(Source(), "docker", Target(), CancellationToken.None));

        Assert.Empty(FolderNamesIn(TargetFile(".claude/skills")));
        Assert.Empty(Directory.GetFileSystemEntries(TargetFile(".claude/skills")));
    }

    [Fact]
    public async Task Replace_swaps_in_the_new_content_and_leaves_no_stale_files_or_helper_folders()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# New\n");
        _sourceProject.WriteFile(".claude/skills/docker/scripts/new.sh", "echo new\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        _targetProject.WriteFile(".claude/skills/docker/old-only.txt", "old\n");
        _targetProject.WriteFile(".claude/skills/untouched/SKILL.md", "# Untouched\n");

        await Copier().ReplaceAsync(Source(), "docker", Target(), CancellationToken.None);

        Assert.Equal("# New\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
        Assert.Equal("echo new\n", File.ReadAllText(TargetFile(".claude/skills/docker/scripts/new.sh")));
        Assert.False(File.Exists(TargetFile(".claude/skills/docker/old-only.txt")));
        Assert.Equal(["docker", "untouched"], FolderNamesIn(TargetFile(".claude/skills")));
        Assert.Empty(Directory.GetFiles(TargetFile(".claude/skills")));
    }

    [Fact]
    public async Task Replace_leaves_the_old_folder_exactly_as_it_was_when_the_source_is_missing()
    {
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        _targetProject.WriteFile(".claude/skills/docker/extra.txt", "extra\n");

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().ReplaceAsync(Source(), "docker", Target(), CancellationToken.None));

        Assert.Equal("# Old\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
        Assert.Equal("extra\n", File.ReadAllText(TargetFile(".claude/skills/docker/extra.txt")));
        Assert.Equal(["docker"], FolderNamesIn(TargetFile(".claude/skills")));
    }

    [Fact]
    public async Task Replace_leaves_the_old_folder_and_no_helper_folders_when_a_file_cannot_be_read()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# New\n");
        _sourceProject.WriteFile(".claude/skills/docker/locked.sh", "echo locked\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Old\n");
        using var heldOpen = new FileStream(
            Path.Combine(_sourceProject.Path, ".claude", "skills", "docker", "locked.sh"),
            FileMode.Open, FileAccess.Read, FileShare.None);

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().ReplaceAsync(Source(), "docker", Target(), CancellationToken.None));

        Assert.Equal("# Old\n", File.ReadAllText(TargetFile(".claude/skills/docker/SKILL.md")));
        Assert.Equal(["docker"], FolderNamesIn(TargetFile(".claude/skills")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("nested/skill")]
    [InlineData("nested\\skill")]
    public async Task Rejects_folder_names_that_could_leave_the_skills_directory(string folderName)
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");

        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().CopyAsync(Source(), folderName, Target(), CancellationToken.None));
        await Assert.ThrowsAsync<SkillCopyFailedException>(
            () => Copier().ReplaceAsync(Source(), folderName, Target(), CancellationToken.None));

        Assert.False(Directory.Exists(TargetFile(".claude")));
    }
}
