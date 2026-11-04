using Scion.Application.Editing;

namespace Scion.Infrastructure.FileSystem.Tests;

public class YamlSkillFrontmatterParserTests
{
    private static SkillFrontmatterFields Parse(string text) => new YamlSkillFrontmatterParser().Parse(text);

    [Fact]
    public void Reads_the_name_and_the_description()
    {
        var fields = Parse("---\nname: pdf\ndescription: Reads PDFs.\n---\n# Body\n");

        Assert.Equal(new SkillFrontmatterFields("pdf", "Reads PDFs.", false), fields);
    }

    [Fact]
    public void Reads_a_header_with_CRLF_line_endings()
    {
        var fields = Parse("---\r\nname: pdf\r\ndescription: Reads PDFs.\r\n---\r\n# Body\r\n");

        Assert.Equal(new SkillFrontmatterFields("pdf", "Reads PDFs.", false), fields);
    }

    [Fact]
    public void Ignores_other_fields()
    {
        var fields = Parse("---\nname: pdf\nlicense: MIT\ndescription: Reads PDFs.\n---\n");

        Assert.Equal(new SkillFrontmatterFields("pdf", "Reads PDFs.", false), fields);
    }

    [Fact]
    public void A_text_without_frontmatter_has_no_fields_and_is_not_unreadable()
    {
        Assert.Equal(new SkillFrontmatterFields(null, null, false), Parse("# Just a body\n"));
    }

    [Fact]
    public void An_empty_text_has_no_fields_and_is_not_unreadable()
    {
        Assert.Equal(new SkillFrontmatterFields(null, null, false), Parse(""));
    }

    [Fact]
    public void An_empty_frontmatter_block_has_no_fields_and_is_not_unreadable()
    {
        Assert.Equal(new SkillFrontmatterFields(null, null, false), Parse("---\n---\n# Body\n"));
    }

    [Fact]
    public void Invalid_YAML_is_unreadable()
    {
        Assert.Equal(
            new SkillFrontmatterFields(null, null, true),
            Parse("---\nname: [unclosed\ndescription: Fine.\n---\n# Body\n"));
    }

    [Fact]
    public void A_block_that_never_closes_is_unreadable()
    {
        Assert.Equal(
            new SkillFrontmatterFields(null, null, true),
            Parse("---\nname: broken\ndescription: Never closed.\n# Body\n"));
    }

    [Fact]
    public void Reads_a_multi_line_description()
    {
        var fields = Parse("---\nname: pdf\ndescription: >\n  Line one.\n  Line two.\n---\n");

        Assert.Equal("Line one. Line two.\n", fields.Description);
    }
}
