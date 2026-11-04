using Scion.Application.Creating;
using Scion.Application.Skills.Copying;

namespace Scion.Application.Tests.Creating;

public class NewSkillFormTests
{
    private const string ValidDescription = "Reviews pull requests.";

    private static NewSkillForm FormWith(
        StringComparison comparison = StringComparison.Ordinal, params string[] existingFolderNames) =>
        new(existingFolderNames, new PathComparison(comparison));

    [Fact]
    public void A_valid_name_and_description_can_be_created()
    {
        var check = FormWith().Check("code-review", ValidDescription);

        Assert.Null(check.NameProblem);
        Assert.Null(check.DescriptionProblem);
        Assert.True(check.CanCreate);
    }

    [Theory]
    [InlineData("", NewSkillProblem.NameEmpty)]
    [InlineData("Code-Review", NewSkillProblem.NameInvalidCharacters)]
    [InlineData("code review", NewSkillProblem.NameInvalidCharacters)]
    [InlineData("-code", NewSkillProblem.NameHyphenAtEdge)]
    [InlineData("code-", NewSkillProblem.NameHyphenAtEdge)]
    [InlineData("code--review", NewSkillProblem.NameDoubleHyphen)]
    public void A_name_that_breaks_the_rules_says_which(string name, NewSkillProblem expected)
    {
        var check = FormWith().Check(name, ValidDescription);

        Assert.Equal(expected, check.NameProblem);
        Assert.False(check.CanCreate);
    }

    [Fact]
    public void A_name_over_the_limit_is_too_long()
    {
        var check = FormWith().Check(new string('a', 65), ValidDescription);

        Assert.Equal(NewSkillProblem.NameTooLong, check.NameProblem);
    }

    [Fact]
    public void A_name_is_not_trimmed_so_a_space_around_it_is_an_error()
    {
        var check = FormWith().Check(" code-review", ValidDescription);

        Assert.Equal(NewSkillProblem.NameInvalidCharacters, check.NameProblem);
    }

    [Fact]
    public void A_name_that_is_already_a_folder_here_is_taken()
    {
        var check = FormWith(StringComparison.Ordinal, "docker", "code-review").Check("code-review", ValidDescription);

        Assert.Equal(NewSkillProblem.NameTaken, check.NameProblem);
        Assert.False(check.CanCreate);
    }

    [Fact]
    public void Taken_follows_the_case_rule_of_the_file_system()
    {
        var caseInsensitive = FormWith(StringComparison.OrdinalIgnoreCase, "Code-Review").Check("code-review", ValidDescription);
        var caseSensitive = FormWith(StringComparison.Ordinal, "Code-Review").Check("code-review", ValidDescription);

        Assert.Equal(NewSkillProblem.NameTaken, caseInsensitive.NameProblem);
        Assert.Null(caseSensitive.NameProblem);
    }

    [Fact]
    public void An_invalid_name_is_reported_before_a_taken_one()
    {
        var check = FormWith(StringComparison.Ordinal, "Docker").Check("Docker", ValidDescription);

        Assert.Equal(NewSkillProblem.NameInvalidCharacters, check.NameProblem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void A_description_with_nothing_but_whitespace_is_empty(string description)
    {
        var check = FormWith().Check("code-review", description);

        Assert.Equal(NewSkillProblem.DescriptionEmpty, check.DescriptionProblem);
        Assert.False(check.CanCreate);
    }

    [Fact]
    public void A_description_over_the_limit_is_too_long()
    {
        var check = FormWith().Check("code-review", new string('a', 1025));

        Assert.Equal(NewSkillProblem.DescriptionTooLong, check.DescriptionProblem);
    }

    [Fact]
    public void The_limit_counts_the_description_after_trimming()
    {
        var check = FormWith().Check("code-review", " " + new string('a', 1024) + " ");

        Assert.Null(check.DescriptionProblem);
    }

    [Fact]
    public void Name_and_description_problems_are_reported_together()
    {
        var check = FormWith().Check("", "");

        Assert.Equal(NewSkillProblem.NameEmpty, check.NameProblem);
        Assert.Equal(NewSkillProblem.DescriptionEmpty, check.DescriptionProblem);
    }
}
