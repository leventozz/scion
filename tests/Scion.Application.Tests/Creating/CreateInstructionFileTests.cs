using Scion.Application.Creating;
using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Application.Tests.Creating;

public class CreateInstructionFileTests
{
    private readonly InMemoryInstructionFileCreation _creation = new();

    private CreateInstructionFile CreateInstructionFile() => new(_creation);

    [Fact]
    public async Task A_project_file_starts_with_the_folder_name_as_its_title()
    {
        var project = new Location.Project("/work/docs-api");

        var result = await CreateInstructionFile().ExecuteAsync(project, Agent.ClaudeCode);

        Assert.Equal("# docs-api\n\n", _creation.Content);
        Assert.Equal(project, _creation.Location);
        Assert.Equal(Agent.ClaudeCode, _creation.Agent);
        Assert.Equal(new EditableFile.InstructionFile(project, Agent.ClaudeCode, "CLAUDE.md"), result.Value);
    }

    [Theory]
    [InlineData("/work/docs-api/", "docs-api")]
    [InlineData(@"C:\work\docs-api\", "docs-api")]
    [InlineData(@"C:\work\docs-api", "docs-api")]
    public async Task The_title_ignores_a_trailing_separator(string path, string expectedTitle)
    {
        await CreateInstructionFile().ExecuteAsync(new Location.Project(path), Agent.Codex);

        Assert.Equal($"# {expectedTitle}\n\n", _creation.Content);
    }

    [Fact]
    public async Task A_personal_file_starts_empty()
    {
        _creation.RelativePath = ".codex/AGENTS.md";

        var result = await CreateInstructionFile().ExecuteAsync(new Location.Personal(), Agent.Codex);

        Assert.Equal("", _creation.Content);
        Assert.Equal(".codex/AGENTS.md", result.Value.RelativePath);
    }

    [Theory]
    [InlineData(InstructionFileCreateFailure.AlreadyExists, CreateInstructionFileError.AlreadyExists)]
    [InlineData(InstructionFileCreateFailure.FolderMissing, CreateInstructionFileError.FolderMissing)]
    [InlineData(InstructionFileCreateFailure.AccessDenied, CreateInstructionFileError.WriteFailed)]
    [InlineData(InstructionFileCreateFailure.Other, CreateInstructionFileError.WriteFailed)]
    public async Task A_failure_of_the_creator_becomes_the_matching_error(
        InstructionFileCreateFailure failure, CreateInstructionFileError expected)
    {
        _creation.FailWith(failure);

        var result = await CreateInstructionFile().ExecuteAsync(new Location.Project("/work/docs-api"), Agent.ClaudeCode);

        Assert.Equal(expected, result.Error);
    }
}
