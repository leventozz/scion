using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

public class ProgramTests
{
    [Fact]
    public void A_clean_site_exits_with_0_and_says_so()
    {
        using var site = new TempSite().With("index.html", "<img src=\"images/a.png\">");
        var output = new StringWriter();

        var exitCode = Program.Run([site.Path], output);

        Assert.Equal(0, exitCode);
        Assert.Contains("no external references", output.ToString());
    }

    [Fact]
    public void An_external_address_exits_with_1_and_prints_a_line_of_file_and_address()
    {
        using var site = new TempSite().With("index.html", "<script src=\"https://cdn.example.test/x.js\"></script>");
        var output = new StringWriter();

        var exitCode = Program.Run([site.Path], output);

        Assert.Equal(1, exitCode);
        Assert.Contains("index.html: https://cdn.example.test/x.js", output.ToString());
    }

    [Fact]
    public void A_missing_folder_exits_with_2()
    {
        var output = new StringWriter();

        var exitCode = Program.Run([Path.Combine(Path.GetTempPath(), "scion-site-check-missing-" + Guid.NewGuid())], output);

        Assert.Equal(2, exitCode);
        Assert.Contains("not found", output.ToString());
    }

    [Theory]
    [InlineData]
    [InlineData("one", "two")]
    public void Without_exactly_one_folder_it_prints_the_usage_and_exits_with_2(params string[] arguments)
    {
        var output = new StringWriter();

        var exitCode = Program.Run(arguments, output);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: Scion.SiteCheck <folder>", output.ToString());
    }
}
