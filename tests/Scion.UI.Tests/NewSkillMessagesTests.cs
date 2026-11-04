using Scion.Application.Creating;

namespace Scion.UI.Tests;

public class NewSkillMessagesTests
{
    [Theory]
    [InlineData(NewSkillProblem.NameInvalidCharacters, "Use lowercase letters, numbers and hyphens.")]
    [InlineData(NewSkillProblem.NameTooLong, "Use at most 64 characters.")]
    [InlineData(NewSkillProblem.NameHyphenAtEdge, "Don't start or end with a hyphen.")]
    [InlineData(NewSkillProblem.NameDoubleHyphen, "Don't use two hyphens in a row.")]
    [InlineData(NewSkillProblem.NameTaken, "A skill with this name already exists here.")]
    [InlineData(NewSkillProblem.DescriptionTooLong, "Use at most 1024 characters.")]
    public void A_problem_is_said_in_a_calm_sentence(NewSkillProblem problem, string expected) =>
        Assert.Equal(expected, NewSkillMessages.For(problem));

    [Theory]
    [InlineData(NewSkillProblem.NameEmpty)]
    [InlineData(NewSkillProblem.DescriptionEmpty)]
    public void Nothing_filled_in_yet_is_not_a_problem_to_shout_about(NewSkillProblem problem) =>
        Assert.Null(NewSkillMessages.For(problem));

    [Fact]
    public void Every_problem_is_known() =>
        Assert.All(Enum.GetValues<NewSkillProblem>(), problem => NewSkillMessages.For(problem));

    [Fact]
    public void A_failed_write_has_its_own_sentence() =>
        Assert.Equal("The skill couldn't be created.", NewSkillMessages.CreateFailed);
}
