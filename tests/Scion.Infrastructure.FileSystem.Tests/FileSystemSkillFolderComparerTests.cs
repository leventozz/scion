using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemSkillFolderComparerTests : IDisposable
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

    private Task<bool> AreIdentical(string folderName = "docker", Agent sourceAgent = Agent.ClaudeCode, Agent targetAgent = Agent.ClaudeCode) =>
        new FileSystemSkillFolderComparer(_sourceProject.Path, [new ClaudeCodeLayout(), new CodexLayout()])
            .AreIdenticalAsync(Source(sourceAgent), folderName, Target(targetAgent), CancellationToken.None);

    [Fact]
    public async Task Folders_with_the_same_files_and_content_are_identical()
    {
        foreach (var project in new[] { _sourceProject, _targetProject })
        {
            project.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
            project.WriteFile(".claude/skills/docker/scripts/run.sh", "echo run\n");
        }

        Assert.True(await AreIdentical());
    }

    [Fact]
    public async Task Different_content_in_one_file_makes_them_different()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker, edited\n");

        Assert.False(await AreIdentical());
    }

    [Fact]
    public async Task Content_that_differs_only_in_a_sub_folder_makes_them_different()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _sourceProject.WriteFile(".claude/skills/docker/scripts/run.sh", "echo one\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".claude/skills/docker/scripts/run.sh", "echo two\n");

        Assert.False(await AreIdentical());
    }

    [Fact]
    public async Task An_extra_file_on_either_side_makes_them_different()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".claude/skills/docker/extra.txt", "extra\n");

        Assert.False(await AreIdentical());

        _sourceProject.WriteFile(".claude/skills/docker/other.txt", "other\n");
        Assert.False(await AreIdentical());
    }

    [Fact]
    public async Task The_same_content_under_another_file_name_is_different()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".claude/skills/docker/NOTES.md", "# Docker\n");

        Assert.False(await AreIdentical());
    }

    [Fact]
    public async Task Compares_binary_files_byte_for_byte()
    {
        await WriteBinaryAsync(_sourceProject, [0x00, 0xFF, 0x0D, 0x0A]);
        await WriteBinaryAsync(_targetProject, [0x00, 0xFF, 0x0D, 0x0A]);
        Assert.True(await AreIdentical());

        await WriteBinaryAsync(_targetProject, [0x00, 0xFF, 0x0A]);
        Assert.False(await AreIdentical());
    }

    [Fact]
    public async Task Compares_the_skills_folders_of_the_given_agents()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        _targetProject.WriteFile(".agents/skills/docker/SKILL.md", "# Docker\n");

        Assert.True(await AreIdentical(sourceAgent: Agent.ClaudeCode, targetAgent: Agent.Codex));
    }

    [Fact]
    public async Task Fails_when_a_folder_is_missing_on_either_side()
    {
        _sourceProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");

        await Assert.ThrowsAsync<SkillCopyFailedException>(() => AreIdentical());

        _targetProject.WriteFile(".claude/skills/docker/SKILL.md", "# Docker\n");
        Directory.Delete(Path.Combine(_sourceProject.Path, ".claude", "skills", "docker"), recursive: true);

        await Assert.ThrowsAsync<SkillCopyFailedException>(() => AreIdentical());
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("nested/skill")]
    public async Task Rejects_folder_names_that_could_leave_the_skills_directory(string folderName)
    {
        await Assert.ThrowsAsync<SkillCopyFailedException>(() => AreIdentical(folderName));
    }

    private static async Task WriteBinaryAsync(TempDirectory project, byte[] bytes)
    {
        var path = Path.Combine(project.Path, ".claude", "skills", "docker", "logo.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }
}
