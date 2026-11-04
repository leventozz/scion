using Scion.Application.Creating;

namespace Scion.Application.Tests.Creating;

public class NewSkillDescriptionTests
{
    [Theory]
    [InlineData("one\ntwo", "one two")]
    [InlineData("one\r\ntwo", "one two")]
    [InlineData("one\rtwo", "one two")]
    public void Line_breaks_become_spaces(string pasted, string expected) =>
        Assert.Equal(expected, NewSkillDescription.SingleLine(pasted));

    [Fact]
    public void Single_line_keeps_the_edges_so_the_user_can_keep_typing() =>
        Assert.Equal(" one ", NewSkillDescription.SingleLine(" one "));

    [Theory]
    [InlineData("  Reviews pull requests.  ", "Reviews pull requests.")]
    [InlineData("\n Reviews\npull requests.\n", "Reviews pull requests.")]
    [InlineData("   ", "")]
    public void Normalize_trims_the_edges_and_removes_line_breaks(string typed, string expected) =>
        Assert.Equal(expected, NewSkillDescription.Normalize(typed));
}
