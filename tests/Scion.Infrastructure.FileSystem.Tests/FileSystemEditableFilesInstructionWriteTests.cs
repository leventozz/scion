using System.Text.RegularExpressions;
using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemEditableFilesInstructionWriteTests : IDisposable
{
    private static readonly byte[] OldBytes = [0x61, 0x0D, 0x0A, 0x62];
    private static readonly byte[] NewBytes = [0xEF, 0xBB, 0xBF, 0x63, 0x0A, 0xC3, 0xA7];

    private readonly TempDirectory _project = new();

    public void Dispose() => _project.Dispose();

    private Location Project => new Location.Project(_project.Path);

    private string InProject(string relativePath) => Path.Combine(_project.Path, relativePath);

    private FileSystemEditableFiles FilesWith(Moves moves) =>
        new(_project.Path, [new ClaudeCodeLayout(), new CodexLayout()], [new ClaudeCodeLayout(), new CodexLayout()], moves.Move);

    private Task WriteAsync(EditableFile file, byte[] content, Moves? moves = null, string? unchangedSinceHash = null) =>
        FilesWith(moves ?? new Moves()).WriteAsync(file, content, unchangedSinceHash, CancellationToken.None);

    private EditableFile.InstructionFile ClaudeMd => new(Project, Agent.ClaudeCode, "CLAUDE.md");

    [Fact]
    public async Task Replaces_the_content_byte_for_byte()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);

        await WriteAsync(ClaudeMd, NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(InProject("CLAUDE.md")));
    }

    [Fact]
    public async Task Does_not_replace_a_file_that_changed_since_the_edit_started_and_leaves_no_temp_file()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);
        var hashWhenOpened = ContentHash.Of(OldBytes);
        File.WriteAllText(InProject("CLAUDE.md"), "changed elsewhere");

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(
            () => WriteAsync(ClaudeMd, NewBytes, unchangedSinceHash: hashWhenOpened));

        Assert.Equal(FileSaveFailure.ChangedOnDisk, failure.Failure);
        Assert.Equal("changed elsewhere", File.ReadAllText(InProject("CLAUDE.md")));
        Assert.Equal(["CLAUDE.md"], Directory.GetFileSystemEntries(_project.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Writes_a_file_that_does_not_exist_yet()
    {
        await WriteAsync(ClaudeMd, NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(InProject("CLAUDE.md")));
    }

    [Fact]
    public async Task Stages_next_to_the_target_as_a_dot_file_with_a_scion_name()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);
        var moves = new Moves();

        await WriteAsync(ClaudeMd, NewBytes, moves);

        var move = Assert.Single(moves.Done);
        Assert.Equal(_project.Path, Path.GetDirectoryName(move.From));
        Assert.Matches(new Regex(@"^\.CLAUDE\.md\.scion-[0-9a-f]{32}\.tmp$"), Path.GetFileName(move.From));
        Assert.Equal(InProject("CLAUDE.md"), move.To);
    }

    [Fact]
    public async Task Leaves_no_temp_file_behind()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);

        await WriteAsync(ClaudeMd, NewBytes);

        Assert.Equal(["CLAUDE.md"], Directory.GetFileSystemEntries(_project.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task A_failed_move_keeps_the_file_intact_and_removes_the_temp_file()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);
        var moves = new Moves(failWith: () => new IOException("Simulated failure of a file move."));

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(ClaudeMd, NewBytes, moves));

        Assert.Equal(FileSaveFailure.Other, failure.Failure);
        Assert.Equal(OldBytes, File.ReadAllBytes(InProject("CLAUDE.md")));
        Assert.Equal(["CLAUDE.md"], Directory.GetFileSystemEntries(_project.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task A_read_only_file_is_not_written()
    {
        File.WriteAllBytes(InProject("CLAUDE.md"), OldBytes);
        File.SetAttributes(InProject("CLAUDE.md"), FileAttributes.ReadOnly);

        try
        {
            var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(ClaudeMd, NewBytes));

            Assert.Equal(FileSaveFailure.AccessDenied, failure.Failure);
            Assert.Equal(OldBytes, File.ReadAllBytes(InProject("CLAUDE.md")));
            Assert.Equal(["CLAUDE.md"], Directory.GetFileSystemEntries(_project.Path).Select(Path.GetFileName));
        }
        finally
        {
            File.SetAttributes(InProject("CLAUDE.md"), FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Writes_the_dot_claude_file_of_a_project()
    {
        _project.WriteFile(".claude/CLAUDE.md", "old");

        await WriteAsync(new EditableFile.InstructionFile(Project, Agent.ClaudeCode, ".claude/CLAUDE.md"), NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(InProject(".claude/CLAUDE.md")));
    }

    [Fact]
    public async Task A_missing_folder_is_a_folder_missing_failure_and_nothing_is_created()
    {
        var file = new EditableFile.InstructionFile(Project, Agent.ClaudeCode, ".claude/CLAUDE.md");

        var failure = await Assert.ThrowsAsync<FileSaveFailedException>(() => WriteAsync(file, NewBytes));

        Assert.Equal(FileSaveFailure.FolderMissing, failure.Failure);
        Assert.Empty(Directory.GetFileSystemEntries(_project.Path));
    }

    [Fact]
    public async Task Writes_the_personal_file_under_the_home_directory()
    {
        _project.WriteFile(".codex/AGENTS.md", "old");

        await WriteAsync(new EditableFile.InstructionFile(new Location.Personal(), Agent.Codex, ".codex/AGENTS.md"), NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(InProject(".codex/AGENTS.md")));
    }

    [Fact]
    public async Task Writes_the_codex_file_of_a_project()
    {
        _project.WriteFile("AGENTS.md", "old");

        await WriteAsync(new EditableFile.InstructionFile(Project, Agent.Codex, "AGENTS.md"), NewBytes);

        Assert.Equal(NewBytes, File.ReadAllBytes(InProject("AGENTS.md")));
    }

    [Theory]
    [InlineData(Agent.ClaudeCode, "AGENTS.md")]
    [InlineData(Agent.Codex, "CLAUDE.md")]
    [InlineData(Agent.ClaudeCode, "../CLAUDE.md")]
    [InlineData(Agent.ClaudeCode, "notes.md")]
    public async Task Refuses_a_file_that_is_not_in_the_convention_of_the_agent(Agent agent, string relativePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => WriteAsync(new EditableFile.InstructionFile(Project, agent, relativePath), NewBytes));

        Assert.Empty(Directory.GetFileSystemEntries(_project.Path));
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
