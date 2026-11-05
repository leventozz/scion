using System.Text.RegularExpressions;

namespace Scion.Infrastructure.FileSystem.Tests;

/// <summary>
/// What an adaptation shares is promised in the README and on the website. These tests tie both texts to the allowlist in
/// <see cref="FileSystemTargetContext"/>: when the list in the code changes, they fail until the texts say the same.
/// </summary>
public class AdaptationAllowlistDocumentationTests
{
    [Fact]
    public void Readme_lists_exactly_the_manifests_an_adaptation_shares()
    {
        var section = ReadmeSection("What Adapt with AI sends");

        var listed = Regex.Match(section, @"project manifests \((?<items>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(listed.Success, "The README section has no 'project manifests (...)' item.");
        Assert.Equal(
            FileSystemTargetContext.SharedManifests.Order(StringComparer.Ordinal),
            Regex.Matches(listed.Groups["items"].Value, "`([^`]+)`").Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Site_lists_exactly_the_manifests_an_adaptation_shares()
    {
        var section = SiteSection("what-adapt-sends");

        var item = Regex.Match(section, @"<li>(?:(?!</li>).)*?[Pp]roject manifests.*?</li>", RegexOptions.Singleline);

        Assert.True(item.Success, "The site section has no 'Project manifests' list item.");
        Assert.Equal(
            FileSystemTargetContext.SharedManifests.Order(StringComparer.Ordinal),
            Regex.Matches(item.Value, "<code>([^<]+)</code>").Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Readme_and_site_say_how_deep_folder_names_go()
    {
        var depth = FileSystemTargetContext.FolderNameDepth switch
        {
            2 => "two levels deep",
            var other => throw new InvalidOperationException($"Add the words for a depth of {other} to this test, the README and the site."),
        };

        Assert.Contains(depth, ReadmeSection("What Adapt with AI sends"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(depth, SiteSection("what-adapt-sends"), StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadmeSection(string heading)
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));
        var match = Regex.Match(readme, $@"^## {Regex.Escape(heading)}\s*$(?<body>.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.True(match.Success, $"README.md has no '## {heading}' section.");

        return match.Groups["body"].Value;
    }

    private static string SiteSection(string id)
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "site", "index.html"));
        var match = Regex.Match(page, $@"<section[^>]*\bid=""{Regex.Escape(id)}""[^>]*>(?<body>.*?)</section>", RegexOptions.Singleline);

        Assert.True(match.Success, $"site/index.html has no <section id=\"{id}\">.");

        return match.Groups["body"].Value;
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
