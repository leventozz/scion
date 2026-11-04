using Scion.Application.Deleting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Deleting;

public class DeleteSkillTests
{
    private static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 30, 15, TimeSpan.FromHours(3));

    private readonly InMemorySkillTrash _trash = new();
    private readonly DeleteSkill _deleteSkill;

    public DeleteSkillTests() => _deleteSkill = new DeleteSkill(_trash, new FixedTimeProvider(Now));

    [Fact]
    public async Task Moves_the_skill_to_the_trash()
    {
        var result = await _deleteSkill.ExecuteAsync(Api, "docs");

        Assert.True(result.IsSuccess);
        Assert.Equal(SkillDeletion.MovedToTrash, result.Value);
        var trashed = Assert.Single(_trash.Trashed);
        Assert.Equal(Api, trashed.Place);
        Assert.Equal("docs", trashed.FolderName);
    }

    [Fact]
    public async Task Hands_the_current_time_to_the_trash()
    {
        await _deleteSkill.ExecuteAsync(Api, "docs");

        Assert.Equal(Now, Assert.Single(_trash.Trashed).Now);
    }

    [Fact]
    public async Task Reports_when_the_original_folder_could_not_be_fully_removed()
    {
        _trash.LeaveOriginalBehind();

        var result = await _deleteSkill.ExecuteAsync(Api, "docs");

        Assert.True(result.IsSuccess);
        Assert.Equal(SkillDeletion.OriginalNotFullyRemoved, result.Value);
    }

    [Theory]
    [InlineData(SkillTrashFailure.Missing, DeleteSkillError.SkillMissing)]
    [InlineData(SkillTrashFailure.Linked, DeleteSkillError.Linked)]
    [InlineData(SkillTrashFailure.Other, DeleteSkillError.Failed)]
    public async Task Maps_each_trash_failure_to_its_error(SkillTrashFailure failure, DeleteSkillError expected)
    {
        _trash.FailWith(failure);

        var result = await _deleteSkill.ExecuteAsync(Api, "docs");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
    }
}
