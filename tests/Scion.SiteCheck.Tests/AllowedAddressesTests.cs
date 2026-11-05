using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

public class AllowedAddressesTests
{
    private static readonly AllowedAddresses Allowed = new(["https://example.test/owner/repo", "https://owner.example.test/repo/"]);

    [Theory]
    [InlineData("https://example.test/owner/repo")]
    [InlineData("https://example.test/owner/repo/")]
    [InlineData("https://example.test/owner/repo/releases/latest/download/Setup.exe")]
    [InlineData("https://example.test/owner/repo?tab=readme")]
    [InlineData("https://example.test/owner/repo#license")]
    [InlineData("https://owner.example.test/repo")]
    [InlineData("HTTPS://EXAMPLE.TEST/owner/repo")]
    public void Allows_an_address_that_is_a_listed_address_or_below_it(string address) =>
        Assert.True(Allowed.Allows(address));

    [Theory]
    [InlineData("https://example.test/owner/repo-other")]
    [InlineData("https://example.test/owner/repository")]
    [InlineData("https://example.test/owner")]
    [InlineData("http://example.test/owner/repo")]
    [InlineData("https://other.example.test/owner/repo")]
    [InlineData("https://example.test.evil.test/owner/repo")]
    [InlineData("//example.test/owner/repo")]
    public void Refuses_everything_else(string address) =>
        Assert.False(Allowed.Allows(address));

    // What the site may link to is spelled out in AllowedAddresses.Site only; that the page's own links pass is
    // WebsiteTests' job, so no test here names the repository.
    [Theory]
    [InlineData("https://example.test/owner/repo")]
    [InlineData("https://github.com/someone-else/scion")]
    [InlineData("https://someone-else.github.io/scion")]
    public void The_site_may_not_link_to_anywhere_else(string address) =>
        Assert.False(AllowedAddresses.Site.Allows(address));
}
