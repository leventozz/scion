using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Scion.UI.Tests;

public class MarkdownViewTests : BunitContext
{
    private readonly FakeExternalLinks _links = new();

    public MarkdownViewTests() => Services.AddSingleton<IExternalLinks>(_links);

    [Fact]
    public void Renders_headings_paragraphs_and_emphasis()
    {
        var view = RenderMarkdown("# Title\n\n## Sub\n\nSome *soft* and **strong** text with `code`.\n");

        Assert.Equal("Title", view.Find("h1").TextContent);
        Assert.Equal("Sub", view.Find("h2").TextContent);
        Assert.Equal("soft", view.Find("p em").TextContent);
        Assert.Equal("strong", view.Find("p strong").TextContent);
        Assert.Equal("code", view.Find("p code").TextContent);
    }

    [Fact]
    public void Renders_unordered_and_ordered_lists()
    {
        var view = RenderMarkdown("- one\n- two\n\n1. first\n2. second\n");

        Assert.Equal(["one", "two"], view.FindAll("ul > li").Select(item => item.TextContent));
        Assert.Equal(["first", "second"], view.FindAll("ol > li").Select(item => item.TextContent));
    }

    [Fact]
    public void A_tight_list_item_has_no_paragraph_but_a_loose_one_does()
    {
        var tight = RenderMarkdown("- one\n- two\n");
        var loose = RenderMarkdown("- one\n\n- two\n");

        Assert.Empty(tight.FindAll("li p"));
        Assert.Equal(2, loose.FindAll("li p").Count);
    }

    [Fact]
    public void Renders_a_fenced_code_block_as_pre_code_with_its_text_unchanged()
    {
        var view = RenderMarkdown("```bash\ndocker compose up\necho \"<done>\"\n```\n");

        var code = view.Find("pre code");
        Assert.Equal("docker compose up\necho \"<done>\"", code.TextContent.TrimEnd('\n'));
        Assert.Contains("code-block", view.Find("pre").ClassList);
    }

    [Fact]
    public void Renders_an_indented_code_block()
    {
        var view = RenderMarkdown("Text\n\n    indented code\n");

        Assert.Equal("indented code", view.Find("pre code").TextContent.TrimEnd('\n'));
    }

    [Fact]
    public void Renders_block_quotes_and_thematic_breaks()
    {
        var view = RenderMarkdown("> quoted\n\n---\n\nafter\n");

        Assert.Equal("quoted", view.Find("blockquote").TextContent.Trim());
        Assert.NotNull(view.Find("hr"));
    }

    [Fact]
    public void Renders_a_pipe_table_with_a_header_row()
    {
        var view = RenderMarkdown("| Name | Value |\n|------|-------|\n| a    | 1     |\n| b    | 2     |\n");

        Assert.NotNull(view.Find(".markdown-table-scroll > table"));
        Assert.Equal(["Name", "Value"], view.FindAll("thead th").Select(cell => cell.TextContent));
        Assert.Equal(["a", "1", "b", "2"], view.FindAll("tbody td").Select(cell => cell.TextContent));
    }

    [Fact]
    public void Raw_html_blocks_appear_as_text_and_never_become_elements()
    {
        var view = RenderMarkdown("<script>alert(1)</script>\n\n<img src=x onerror=\"alert(2)\">\n");

        Assert.Empty(view.FindAll("script"));
        Assert.Empty(view.FindAll("img"));
        Assert.Contains("<script>alert(1)</script>", view.Markup.Replace("&lt;", "<").Replace("&gt;", ">"));
        Assert.Contains("onerror", view.Find(".markdown").TextContent);
    }

    [Fact]
    public void Raw_inline_html_appears_as_text()
    {
        var view = RenderMarkdown("Click <b onclick=\"x()\">here</b> now\n");

        Assert.Empty(view.FindAll("b"));
        Assert.Contains("<b onclick=\"x()\">here</b>", view.Find("p").TextContent);
    }

    [Fact]
    public async Task An_http_link_is_an_anchor_without_href_that_asks_the_port_to_open_it()
    {
        var view = RenderMarkdown("See [the docs](https://example.com/docs?a=1).\n");

        var link = view.Find("p a");
        Assert.Equal("the docs", link.TextContent);
        Assert.False(link.HasAttribute("href"));
        Assert.Equal("0", link.GetAttribute("tabindex"));

        await link.ClickAsync();

        Assert.Equal([new Uri("https://example.com/docs?a=1")], _links.Opened);
    }

    [Fact]
    public async Task Pressing_Enter_on_a_link_opens_it_too()
    {
        var view = RenderMarkdown("[docs](http://example.com/)\n");

        await view.Find("a").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([new Uri("http://example.com/")], _links.Opened);
    }

    [Fact]
    public async Task Other_keys_do_not_open_a_link()
    {
        var view = RenderMarkdown("[docs](http://example.com/)\n");

        await view.Find("a").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        Assert.Empty(_links.Opened);
    }

    [Fact]
    public void An_autolink_is_opened_like_any_http_link()
    {
        var view = RenderMarkdown("Go to <https://example.com> please\n");

        Assert.Equal("https://example.com", view.Find("p a").TextContent);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("./references/other.md")]
    [InlineData("#section")]
    [InlineData("/absolute/path")]
    public void Every_other_link_is_an_inert_span_without_handlers_or_href(string url)
    {
        var view = RenderMarkdown($"[label]({url})\n");

        Assert.Empty(view.FindAll("a"));
        var inert = view.Find("p span.markdown-link-inert");
        Assert.Equal("label", inert.TextContent);
        Assert.DoesNotContain(
            inert.Attributes, attribute => attribute.Name is "href" or "tabindex" || attribute.Name.StartsWith("blazor:"));
    }

    [Fact]
    public void An_image_shows_its_alt_text_and_is_never_loaded()
    {
        var view = RenderMarkdown("![architecture diagram](https://example.com/a.png)\n");

        Assert.Empty(view.FindAll("img"));
        Assert.Equal("architecture diagram", view.Find("p").TextContent);
    }

    [Fact]
    public void An_html_entity_is_decoded_to_its_character()
    {
        var view = RenderMarkdown("Fish &amp; chips &copy;\n");

        Assert.Equal("Fish & chips ©", view.Find("p").TextContent);
    }

    [Fact]
    public void A_hard_line_break_becomes_a_br()
    {
        var view = RenderMarkdown("first  \nsecond\n");

        Assert.Single(view.FindAll("p br"));
    }

    [Fact]
    public void Empty_text_renders_an_empty_container()
    {
        var view = RenderMarkdown("");

        Assert.Empty(view.Find(".markdown").Children);
    }

    [Fact]
    public void Text_inside_a_link_keeps_its_emphasis()
    {
        var view = RenderMarkdown("[**bold** link](https://example.com)\n");

        Assert.Equal("bold", view.Find("a strong").TextContent);
    }

    private IRenderedComponent<MarkdownView> RenderMarkdown(string text) =>
        Render<MarkdownView>(parameters => parameters.Add(view => view.Text, text));
}
