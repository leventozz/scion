using Scion.Domain.Skills;

namespace Scion.Infrastructure.FileSystem.Tests;

public class YamlSkillFrontmatterWriterTests
{
    private static string Write(string name, string description) =>
        new YamlSkillFrontmatterWriter().Write(SkillName.Create(name).Value, SkillDescription.Create(description).Value);

    [Fact]
    public void Writes_a_plain_description_without_quotes_between_two_delimiters_with_LF_line_endings() =>
        Assert.Equal(
            "---\nname: code-review\ndescription: Reviews pull requests before they are merged.\n---\n",
            Write("code-review", "Reviews pull requests before they are merged."));

    [Theory]
    [InlineData("Use when: the user asks for a review")]
    [InlineData("Notes # not a comment")]
    [InlineData("# starts with a hash")]
    [InlineData("\"quoted\" start")]
    [InlineData("'single' start")]
    [InlineData("- looks like a list item")]
    [InlineData("[not] a flow sequence")]
    [InlineData("{not: a map}")]
    [InlineData("&anchor and *alias")]
    [InlineData("!tag at the start")]
    [InlineData("| pipe")]
    [InlineData("> fold")]
    [InlineData("%directive")]
    [InlineData("@at")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("1.5")]
    [InlineData("~")]
    [InlineData("ends with a colon:")]
    [InlineData("back\\slash and \"quotes\"")]
    [InlineData("Türkçe açıklama: çok güzel")]
    public void A_tricky_description_reads_back_as_it_was_written(string description)
    {
        var text = Write("code-review", description) + "# Body\n";

        var read = new YamlSkillFrontmatterParser().Parse(text);

        Assert.Equal("code-review", read.Name);
        Assert.Equal(description, read.Description);
        Assert.False(read.IsUnreadable);
    }

    [Fact]
    public void A_long_description_stays_on_one_line_and_reads_back()
    {
        var description = string.Join(' ', Enumerable.Repeat("Reviews pull requests carefully.", 30));

        var text = Write("code-review", description);

        Assert.Equal(4, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(description, new YamlSkillFrontmatterParser().Parse(text).Description);
    }

    [Fact]
    public void Keeps_non_ascii_characters_readable()
    {
        var text = Write("code-review", "Türkçe açıklama");

        Assert.Contains("Türkçe açıklama", text);
    }
}
