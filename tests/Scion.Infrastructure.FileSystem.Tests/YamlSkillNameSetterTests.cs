namespace Scion.Infrastructure.FileSystem.Tests;

public class YamlSkillNameSetterTests
{
    private static readonly YamlSkillNameSetter Setter = new();

    [Fact]
    public void Replaces_the_name_and_leaves_every_other_byte_alone()
    {
        const string text = "---\n# my header\nname: other-name\ndescription: >\n  Does things.\n  More.\nlicense: MIT\n---\n# Title\n\nBody with name: not-a-key\n";

        var result = Setter.WithName(text, "docker");

        Assert.Equal(text.Replace("other-name", "docker"), result);
    }

    [Fact]
    public void Keeps_CRLF_line_breaks()
    {
        const string text = "---\r\nname: other\r\ndescription: x\r\n---\r\nBody\r\n";

        Assert.Equal(text.Replace("other", "docker"), Setter.WithName(text, "docker"));
    }

    [Fact]
    public void Keeps_a_comment_after_the_name()
    {
        Assert.Equal(
            "---\nname: docker # the id\n---\n",
            Setter.WithName("---\nname: other # the id\n---\n", "docker"));
    }

    [Theory]
    [InlineData("name: \"other\"")]
    [InlineData("name: 'other'")]
    public void Replaces_a_quoted_name_with_the_new_one(string line)
    {
        Assert.Equal("---\nname: docker\n---\n", Setter.WithName($"---\n{line}\n---\n", "docker"));
    }

    [Fact]
    public void Sets_an_empty_name()
    {
        var result = Setter.WithName("---\nname:\ndescription: x\n---\n", "docker");

        Assert.Equal("---\nname: docker\ndescription: x\n---\n", result);
    }

    [Fact]
    public void Adds_the_name_as_the_first_line_of_the_frontmatter_when_there_is_none()
    {
        var result = Setter.WithName("---\ndescription: x\n---\nBody\n", "docker");

        Assert.Equal("---\nname: docker\ndescription: x\n---\nBody\n", result);
    }

    [Fact]
    public void Adds_the_name_to_an_empty_frontmatter()
    {
        Assert.Equal("---\nname: docker\n---\nBody\n", Setter.WithName("---\n---\nBody\n", "docker"));
    }

    [Fact]
    public void Adds_the_name_with_the_line_ending_of_the_text()
    {
        Assert.Equal(
            "---\r\nname: docker\r\ndescription: x\r\n---\r\n",
            Setter.WithName("---\r\ndescription: x\r\n---\r\n", "docker"));
    }

    [Fact]
    public void Leaves_a_nested_name_alone_and_adds_the_top_level_one()
    {
        var result = Setter.WithName("---\nmetadata:\n  name: inner\n---\n", "docker");

        Assert.Equal("---\nname: docker\nmetadata:\n  name: inner\n---\n", result);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("a: b")]
    [InlineData("say \"hi\"")]
    [InlineData("#tag")]
    public void Quotes_a_name_that_would_not_read_back_as_the_same_text(string name)
    {
        var result = Setter.WithName("---\nname: old\ndescription: x\n---\n", name);

        Assert.Equal(name, new YamlSkillFrontmatterParser().Parse(result!).Name);
    }

    [Theory]
    [InlineData("A skill without a header\n")]
    [InlineData("---\nname: never closed\n")]
    [InlineData("---\nname: [unclosed\n---\n")]
    [InlineData("---\njust a scalar\n---\n")]
    [InlineData("")]
    public void Gives_null_when_there_is_no_readable_frontmatter(string text)
    {
        Assert.Null(Setter.WithName(text, "docker"));
    }

    [Fact]
    public void Gives_a_result_the_skill_reader_reads_back_with_the_new_name_and_the_old_description()
    {
        var result = Setter.WithName("---\nname: other\ndescription: Does things.\n---\nBody\n", "docker");

        var fields = new YamlSkillFrontmatterParser().Parse(result!);

        Assert.Equal(("docker", "Does things."), (fields.Name, fields.Description));
    }
}
