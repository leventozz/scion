using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

public class SiteScanTests
{
    private static readonly AllowedAddresses Allowed = new(["https://example.test/owner/repo"]);

    [Theory]
    [InlineData("<a href=\"images/a.png\">")]
    [InlineData("<a href=\"/images/a.png\">")]
    [InlineData("<a href=\"#top\">")]
    [InlineData("<a href=\"\">")]
    [InlineData("<a href=\"https://example.test/owner/repo/releases\">")]
    public void Lets_through_a_relative_address_and_an_allowed_one(string html)
    {
        using var site = new TempSite().With("index.html", html);

        Assert.Empty(SiteScan.Run(site.Path, Allowed));
    }

    [Theory]
    [InlineData("https://cdn.example.test/x.js")]
    [InlineData("http://example.test/owner/repo")]
    [InlineData("//cdn.example.test/x.js")]
    [InlineData("""\\cdn.example.test\x.js""")]
    [InlineData("""/\cdn.example.test/x.js""")]
    [InlineData("data:text/javascript,alert(1)")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@example.test")]
    [InlineData("https://example.test/owner/repo-other")]
    public void Reports_an_address_with_a_scheme_or_a_host_that_is_not_allowed(string address)
    {
        using var site = new TempSite().With("index.html", $"<a href=\"{address}\">");

        var violation = Assert.Single(SiteScan.Run(site.Path, Allowed));

        Assert.Equal(new ExternalReference("index.html", address), violation);
    }

    [Fact]
    public void Checks_html_and_css_files_in_every_folder_and_names_the_file()
    {
        using var site = new TempSite()
            .With("index.html", "<p>fine</p>")
            .With("styles.css", "a { background: url(https://cdn.example.test/a.png) }")
            .With("docs/page.html", "<script src=\"https://cdn.example.test/b.js\"></script>");

        var violations = SiteScan.Run(site.Path, Allowed);

        Assert.Equal(
            [new ExternalReference("docs/page.html", "https://cdn.example.test/b.js"), new ExternalReference("styles.css", "https://cdn.example.test/a.png")],
            violations.OrderBy(violation => violation.File, StringComparer.Ordinal));
    }

    [Fact]
    public void Ignores_files_that_are_neither_html_nor_css()
    {
        using var site = new TempSite()
            .With("notes.txt", "https://cdn.example.test/x.js")
            .With("data.json", "{ \"src\": \"https://cdn.example.test/x.js\" }")
            .With("favicon.svg", "<svg><image href=\"https://cdn.example.test/x.png\"/></svg>");

        Assert.Empty(SiteScan.Run(site.Path, Allowed));
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_an_error_not_an_empty_site()
    {
        var missing = Path.Combine(Path.GetTempPath(), "scion-site-check-missing-" + Guid.NewGuid());

        Assert.Throws<DirectoryNotFoundException>(() => SiteScan.Run(missing, Allowed));
    }
}
