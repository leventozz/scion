using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

/// <summary>The website in this repository: <c>site/</c> is a source folder, published together with the files it shares (see pages.yml).</summary>
public class WebsiteTests
{
    [Fact]
    public void Site_has_no_external_references()
    {
        var violations = SiteScan.Run(Path.Combine(RepositoryRoot(), "site"), AllowedAddresses.Site);

        Assert.Empty(violations);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scion.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("scion.slnx not found.");
    }
}
