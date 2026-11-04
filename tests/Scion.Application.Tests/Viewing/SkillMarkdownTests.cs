using Scion.Application.Viewing;

namespace Scion.Application.Tests.Viewing;

public class SkillMarkdownTests
{
    [Fact]
    public void Drops_the_frontmatter_and_keeps_the_body()
    {
        var text = "---\nname: pdf\ndescription: Reads PDFs.\n---\n# PDF\n\nBody.\n";

        Assert.Equal("# PDF\n\nBody.\n", SkillMarkdown.BodyOf(text));
    }

    [Fact]
    public void Understands_windows_line_endings()
    {
        var text = "---\r\nname: pdf\r\n---\r\n# PDF\r\n";

        Assert.Equal("# PDF\r\n", SkillMarkdown.BodyOf(text));
    }

    [Fact]
    public void Without_frontmatter_the_whole_text_is_the_body()
    {
        Assert.Equal("# Just markdown\n", SkillMarkdown.BodyOf("# Just markdown\n"));
    }

    [Fact]
    public void An_unclosed_frontmatter_gives_the_whole_text_so_the_user_sees_the_raw_file()
    {
        var text = "---\nname: pdf\n# Never closed\n";

        Assert.Equal(text, SkillMarkdown.BodyOf(text));
    }

    [Fact]
    public void Frontmatter_must_start_on_the_first_line()
    {
        var text = "\n---\nname: pdf\n---\nBody\n";

        Assert.Equal(text, SkillMarkdown.BodyOf(text));
    }

    [Fact]
    public void A_frontmatter_with_nothing_after_it_gives_an_empty_body()
    {
        Assert.Equal("", SkillMarkdown.BodyOf("---\nname: pdf\n---"));
    }

    [Fact]
    public void Only_the_first_closing_delimiter_ends_the_frontmatter()
    {
        var text = "---\nname: pdf\n---\nIntro\n---\nNot frontmatter\n";

        Assert.Equal("Intro\n---\nNot frontmatter\n", SkillMarkdown.BodyOf(text));
    }
}
