using Scion.Domain.Skills;

namespace Scion.Domain.Tests.Skills;

public class SkillNameTests
{
    [Theory]
    [InlineData("docker-compose-validator")]
    [InlineData("a")]
    public void Accepts_a_valid_name(string input)
    {
        var result = SkillName.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Value.Value);
    }

    [Fact]
    public void Accepts_a_name_of_exactly_64_characters()
    {
        var input = new string('a', 64);

        var result = SkillName.Create(input);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Rejects_a_name_longer_than_64_characters()
    {
        var result = SkillName.Create(new string('a', 65));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.TooLong, result.Error);
    }

    [Fact]
    public void Rejects_uppercase_letters()
    {
        var result = SkillName.Create("Docker-compose");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.UppercaseLetter, result.Error);
    }

    [Theory]
    [InlineData("-docker")]
    [InlineData("docker-")]
    public void Rejects_a_leading_or_trailing_hyphen(string input)
    {
        var result = SkillName.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.LeadingOrTrailingHyphen, result.Error);
    }

    [Fact]
    public void Rejects_consecutive_hyphens()
    {
        var result = SkillName.Create("docker--compose");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.ConsecutiveHyphens, result.Error);
    }

    [Theory]
    [InlineData("docker_compose")]
    [InlineData("docker compose")]
    [InlineData("café")]
    public void Rejects_disallowed_characters(string input)
    {
        var result = SkillName.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.DisallowedCharacter, result.Error);
    }

    [Fact]
    public void Rejects_an_empty_name()
    {
        var result = SkillName.Create("");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillNameError.Empty, result.Error);
    }
}
