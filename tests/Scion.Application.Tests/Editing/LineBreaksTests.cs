using Scion.Application.Editing;

namespace Scion.Application.Tests.Editing;

public class LineBreaksTests
{
    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("a\r\nb\rc\nd", "a\nb\nc\nd")]
    [InlineData("a\r\n\r\nb", "a\n\nb")]
    [InlineData("", "")]
    public void Turns_every_line_break_into_a_single_LF(string text, string expected) =>
        Assert.Equal(expected, LineBreaks.Normalize(text));
}
