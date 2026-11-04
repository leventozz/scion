using Scion.Domain.Skills;

namespace Scion.Domain.Tests.Skills;

public class SkillDescriptionTests
{
    [Fact]
    public void Accepts_a_valid_description()
    {
        const string input = "Validates docker-compose files before they are committed.";

        var result = SkillDescription.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Value.Value);
    }

    [Fact]
    public void Rejects_an_empty_description()
    {
        var result = SkillDescription.Create("");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillDescriptionError.Empty, result.Error);
    }

    [Fact]
    public void Rejects_a_description_longer_than_1024_characters()
    {
        var result = SkillDescription.Create(new string('a', 1025));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillDescriptionError.TooLong, result.Error);
    }

    [Fact]
    public void Accepts_a_description_of_exactly_1024_characters()
    {
        var result = SkillDescription.Create(new string('a', 1024));

        Assert.True(result.IsSuccess);
    }
}
