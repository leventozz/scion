using System.Text;
using Scion.Application.Creating;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemInstructionFileCreatorTests : IDisposable
{
    private const string Content = "# Açıklama\n\n";

    private readonly TempDirectory _home = new();
    private readonly TempDirectory _project = new();

    public void Dispose()
    {
        _home.Dispose();
        _project.Dispose();
    }

    private Location Project => new Location.Project(_project.Path);

    private FileSystemInstructionFileCreator Creator(Action<string, string>? moveFile = null) =>
        new(_home.Path, [new ClaudeCodeLayout(), new CodexLayout()], moveFile ?? ((from, to) => File.Move(from, to, overwrite: false)));

    private Task<string> CreateAsync(Location location, Agent agent, Action<string, string>? moveFile = null) =>
        Creator(moveFile).CreateAsync(location, agent, Content, CancellationToken.None);

    private static async Task<InstructionFileCreateFailure> FailureOf(Task<string> create) =>
        (await Assert.ThrowsAsync<InstructionFileCreateFailedException>(() => create)).Failure;

    private static string[] FileNames(string directory) =>
        Directory.GetFileSystemEntries(directory).Select(entry => Path.GetFileName(entry)!).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task A_project_file_for_Claude_Code_is_CLAUDE_md_in_the_project_root()
    {
        var relativePath = await CreateAsync(Project, Agent.ClaudeCode);

        Assert.Equal("CLAUDE.md", relativePath);
        Assert.Equal(new UTF8Encoding(false).GetBytes(Content), File.ReadAllBytes(Path.Combine(_project.Path, "CLAUDE.md")));
    }

    [Fact]
    public async Task A_project_file_for_Codex_is_AGENTS_md_in_the_project_root()
    {
        var relativePath = await CreateAsync(Project, Agent.Codex);

        Assert.Equal("AGENTS.md", relativePath);
        Assert.True(File.Exists(Path.Combine(_project.Path, "AGENTS.md")));
    }

    [Theory]
    [InlineData(Agent.ClaudeCode, ".claude", "CLAUDE.md")]
    [InlineData(Agent.Codex, ".codex", "AGENTS.md")]
    public async Task A_personal_file_is_made_in_the_agent_folder_of_the_home_directory_even_if_it_does_not_exist_yet(
        Agent agent, string folder, string fileName)
    {
        var relativePath = await CreateAsync(new Location.Personal(), agent);

        Assert.Equal($"{folder}/{fileName}", relativePath);
        Assert.Equal(Content, File.ReadAllText(Path.Combine(_home.Path, folder, fileName)));
    }

    [Fact]
    public async Task Leaves_no_temp_file_behind()
    {
        await CreateAsync(Project, Agent.ClaudeCode);

        Assert.Equal(["CLAUDE.md"], FileNames(_project.Path));
    }

    [Fact]
    public async Task A_project_folder_that_is_gone_is_reported_and_not_recreated()
    {
        var gone = Path.Combine(_project.Path, "gone");

        var failure = await FailureOf(CreateAsync(new Location.Project(gone), Agent.ClaudeCode));

        Assert.Equal(InstructionFileCreateFailure.FolderMissing, failure);
        Assert.False(Directory.Exists(gone));
    }

    [Fact]
    public async Task Never_touches_a_file_that_is_already_there()
    {
        _project.WriteFile("CLAUDE.md", "# Mine\n");

        var failure = await FailureOf(CreateAsync(Project, Agent.ClaudeCode));

        Assert.Equal(InstructionFileCreateFailure.AlreadyExists, failure);
        Assert.Equal("# Mine\n", File.ReadAllText(Path.Combine(_project.Path, "CLAUDE.md")));
        Assert.Equal(["CLAUDE.md"], FileNames(_project.Path));
    }

    [Fact]
    public async Task Another_file_the_agent_reads_counts_as_already_there()
    {
        _project.WriteFile(".claude/CLAUDE.md", "# Mine\n");

        var failure = await FailureOf(CreateAsync(Project, Agent.ClaudeCode));

        Assert.Equal(InstructionFileCreateFailure.AlreadyExists, failure);
        Assert.False(File.Exists(Path.Combine(_project.Path, "CLAUDE.md")));
    }

    [Fact]
    public async Task A_file_that_appears_during_the_move_is_kept_and_the_temp_file_goes()
    {
        var failure = await FailureOf(CreateAsync(Project, Agent.ClaudeCode, (_, target) =>
        {
            File.WriteAllText(target, "# Appeared\n");
            throw new IOException("The target appeared.");
        }));

        Assert.Equal(InstructionFileCreateFailure.AlreadyExists, failure);
        Assert.Equal("# Appeared\n", File.ReadAllText(Path.Combine(_project.Path, "CLAUDE.md")));
        Assert.Equal(["CLAUDE.md"], FileNames(_project.Path));
    }

    [Fact]
    public async Task A_move_that_fails_for_another_reason_is_a_failed_write_and_leaves_nothing()
    {
        var failure = await FailureOf(CreateAsync(Project, Agent.ClaudeCode, (_, _) => throw new IOException("Disk full.")));

        Assert.Equal(InstructionFileCreateFailure.Other, failure);
        Assert.Empty(FileNames(_project.Path));
    }

    [Fact]
    public async Task A_move_that_is_not_allowed_is_access_denied()
    {
        var failure = await FailureOf(CreateAsync(Project, Agent.ClaudeCode, (_, _) => throw new UnauthorizedAccessException()));

        Assert.Equal(InstructionFileCreateFailure.AccessDenied, failure);
        Assert.Empty(FileNames(_project.Path));
    }
}
