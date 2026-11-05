using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

public class ReferenceFinderTests
{
    [Theory]
    [InlineData("<script src=\"https://cdn.example.test/x.js\"></script>")]
    [InlineData("<link href=\"https://cdn.example.test/x.js\" rel=\"stylesheet\">")]
    [InlineData("<img srcset=\"https://cdn.example.test/x.js 2x\">")]
    [InlineData("<a style=\"background: url(https://cdn.example.test/x.js)\">")]
    [InlineData("<style>@import \"https://cdn.example.test/x.js\";</style>")]
    [InlineData("<style>@import url(https://cdn.example.test/x.js);</style>")]
    public void Finds_an_address_wherever_a_page_loads_one(string source) =>
        Assert.Equal(["https://cdn.example.test/x.js"], ReferenceFinder.Find(source));

    [Theory]
    [InlineData("<img src=\"a.png\">")]
    [InlineData("<img src='a.png'>")]
    [InlineData("<img src=a.png>")]
    [InlineData("<img src = \"a.png\" >")]
    [InlineData("<IMG SRC=\"a.png\">")]
    [InlineData("<img\n  src=\"a.png\"\n  alt=\"x\">")]
    public void Reads_every_way_to_quote_an_attribute(string source) =>
        Assert.Equal(["a.png"], ReferenceFinder.Find(source));

    [Fact]
    public void Splits_a_srcset_into_its_addresses_and_drops_the_descriptors()
    {
        var found = ReferenceFinder.Find("<img srcset=\"a.png 1x, b.png 2x,c.png 480w\">");

        Assert.Equal(["a.png", "b.png", "c.png"], found);
    }

    [Fact]
    public void Keeps_a_data_address_in_a_srcset_whole()
    {
        var found = ReferenceFinder.Find("<img srcset=\"data:image/png;base64,AAAA 1x, b.png 2x\">");

        Assert.Equal(["data:image/png;base64,AAAA", "b.png"], found);
    }

    [Theory]
    [InlineData("a { background: url(\"x.png\") }")]
    [InlineData("a { background: url('x.png') }")]
    [InlineData("a { background: url( x.png ) }")]
    [InlineData("a { background: URL(x.png) }")]
    [InlineData("@import \"x.png\";")]
    [InlineData("@import 'x.png';")]
    [InlineData("@import url(x.png);")]
    public void Reads_the_addresses_of_a_stylesheet(string source) =>
        Assert.Equal(["x.png"], ReferenceFinder.Find(source));

    [Fact]
    public void Finds_a_font_face_source_with_several_urls()
    {
        var found = ReferenceFinder.Find("@font-face { src: url(a.woff2) format(\"woff2\"), url(b.woff) format(\"woff\"); }");

        Assert.Equal(["a.woff2", "b.woff"], found);
    }

    [Fact]
    public void Does_not_take_an_attribute_that_only_ends_in_src()
    {
        Assert.Empty(ReferenceFinder.Find("<div data-src=\"https://cdn.example.test/x.js\" data-href=\"https://cdn.example.test/y\"></div>"));
    }

    [Fact]
    public void Finds_nothing_in_text_without_addresses() =>
        Assert.Empty(ReferenceFinder.Find("<p>Scion keeps skills per project.</p>"));
}
