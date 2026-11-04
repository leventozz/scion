using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class AiProviderIdTests
{
    [Fact]
    public void Carries_its_value() =>
        Assert.Equal("sample", new AiProviderId("sample").Value);

    [Fact]
    public void Ids_with_the_same_value_are_equal() =>
        Assert.Equal(new AiProviderId("sample"), new AiProviderId("sample"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_an_empty_value(string value) =>
        Assert.Throws<ArgumentException>(() => new AiProviderId(value));

    [Fact]
    public void Rejects_a_missing_value() =>
        Assert.ThrowsAny<ArgumentException>(() => new AiProviderId(null!));
}
