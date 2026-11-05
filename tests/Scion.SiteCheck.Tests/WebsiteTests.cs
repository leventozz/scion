using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Scion.SiteCheck;

namespace Scion.SiteCheck.Tests;

/// <summary>The website in this repository: <c>site/</c> is a source folder, published together with the files it shares (see pages.yml).</summary>
public class WebsiteTests
{
    private const double MinimumContrast = 4.5;

    [Fact]
    public void Site_has_no_external_references()
    {
        var violations = SiteScan.Run(Path.Combine(RepositoryRoot(), "site"), AllowedAddresses.Site);

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_image_has_alt_text_and_a_size_and_is_one_of_the_shared_images()
    {
        var images = Tags("img");

        Assert.NotEmpty(images);
        foreach (var image in images)
        {
            Assert.False(string.IsNullOrWhiteSpace(Attribute(image, "alt")), $"No alt text: {image}");
            Assert.True(int.TryParse(Attribute(image, "width"), out _), $"No width: {image}");
            Assert.True(int.TryParse(Attribute(image, "height"), out _), $"No height: {image}");

            var source = Attribute(image, "src") ?? "";
            Assert.StartsWith("images/", source);
            Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "docs", "images", source["images/".Length..])), $"docs/{source} does not exist.");
        }
    }

    [Fact]
    public void The_hero_links_the_files_of_the_latest_release_and_release_yml_makes_each_of_them()
    {
        var downloads = Regex.Matches(Page(), @"href=""(?<address>[^""]*/releases/latest/download/(?<file>[^""/]+))""")
            .Select(match => (Address: match.Groups["address"].Value, File: match.Groups["file"].Value))
            .ToList();

        Assert.Equal(["Scion-osx-arm64-Portable.zip", "Scion-osx-x64-Portable.zip", "Scion-win-Setup.exe"], downloads.Select(download => download.File).Order(StringComparer.Ordinal));
        Assert.All(downloads, download => Assert.True(AllowedAddresses.Site.Allows(download.Address), $"{download.Address} is not the repository's."));

        var release = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "release.yml"));
        Assert.All(downloads, download => Assert.Contains(download.File, release));
    }

    [Fact]
    public void The_macOS_download_says_which_Macs_it_is_for_and_how_to_open_an_app_without_a_developer_signature()
    {
        const string RemoveQuarantine = "xattr -dr com.apple.quarantine Scion.app";
        var root = RepositoryRoot();

        Assert.Contains("Apple silicon", PageText());
        Assert.Contains(RemoveQuarantine, PageText());
        Assert.Contains(RemoveQuarantine, File.ReadAllText(Path.Combine(root, "README.md")));
        Assert.Contains(RemoveQuarantine, File.ReadAllText(Path.Combine(root, ".github", "workflows", "package-macos.yml")));
    }

    [Fact]
    public void The_Intel_build_is_offered_with_the_same_words_everywhere_about_not_having_been_tried()
    {
        const string NotTried = "not tried on an Intel Mac yet";
        // A line break in Markdown is a space.
        var readme = Regex.Replace(File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md")), @"\s+", " ");

        Assert.Contains(NotTried, PageText());
        Assert.Contains(NotTried, readme);
    }

    [Fact]
    public void Only_the_first_image_loads_eagerly()
    {
        var images = Tags("img");

        Assert.NotEqual("lazy", Attribute(images[0], "loading"));
        Assert.All(images.Skip(1), image => Assert.Equal("lazy", Attribute(image, "loading")));
    }

    [Fact]
    public void Head_describes_the_page_for_search_and_for_link_previews()
    {
        var canonical = Attribute(LinkTag("canonical"), "href");

        Assert.False(string.IsNullOrWhiteSpace(Attribute(MetaTag("name", "description"), "content")));
        Assert.Equal("website", Attribute(MetaTag("property", "og:type"), "content"));
        Assert.False(string.IsNullOrWhiteSpace(Attribute(MetaTag("property", "og:title"), "content")));
        Assert.False(string.IsNullOrWhiteSpace(Attribute(MetaTag("property", "og:description"), "content")));
        Assert.False(string.IsNullOrWhiteSpace(Attribute(MetaTag("property", "og:image:alt"), "content")));
        Assert.False(string.IsNullOrWhiteSpace(canonical));
        Assert.True(AllowedAddresses.Site.Allows(canonical!), "The canonical address is not the website's own.");
        Assert.EndsWith("/", canonical);
        Assert.Equal(canonical, Attribute(MetaTag("property", "og:url"), "content"));
        Assert.Equal(canonical + "images/project-grid.png", Attribute(MetaTag("property", "og:image"), "content"));
        Assert.Equal("favicon.svg", Attribute(LinkTag("icon"), "href"));
    }

    [Theory]
    [InlineData("Browse", "Every project's skills, Claude Code and Codex side by side. Open a skill to read it, or the project's CLAUDE.md and AGENTS.md.")]
    [InlineData("Edit and create", "Edit a SKILL.md or an instruction file in place, with live checks on the frontmatter. Every save keeps the previous version.")]
    [InlineData("Copy", "Copy skills to another project or agent. If a skill with the same name is already there, you decide: skip or overwrite.")]
    [InlineData("Adapt with AI", "Let Claude Code or Codex rewrite a skill for another project's stack. You review the changes side by side before anything is saved.")]
    public void Each_feature_says_exactly_what_it_was_given(string heading, string text)
    {
        Assert.Contains($"{heading} {text}", PageText());
    }

    [Theory]
    [InlineData("Scion never writes its own files into your repositories.")]
    [InlineData("Remove from Scion never deletes a folder.")]
    [InlineData("Delete moves a skill to Scion's trash.")]
    [InlineData("Nothing is overwritten without your decision.")]
    [InlineData("Every save keeps the previous version.")]
    public void Safe_by_default_lists_its_five_promises(string promise)
    {
        Assert.Contains(promise, PageText());
    }

    [Fact]
    public void Headings_go_down_one_level_at_a_time_below_a_single_h1()
    {
        var levels = Regex.Matches(Page(), @"<h([1-6])\b").Select(match => int.Parse(match.Groups[1].Value)).ToList();

        Assert.Equal(1, levels.Count(level => level == 1));
        Assert.Equal(1, levels[0]);
        Assert.All(levels.Zip(levels.Skip(1)), pair => Assert.True(pair.Second <= pair.First + 1, $"h{pair.First} is followed by h{pair.Second}."));
    }

    [Theory]
    [InlineData("header")]
    [InlineData("main")]
    [InlineData("footer")]
    public void The_page_has_one_landmark_of_each_kind(string element)
    {
        Assert.Single(Tags(element));
    }

    [Fact]
    public void Keyboard_focus_is_visible()
    {
        Assert.Contains(":focus-visible", Styles());
    }

    [Fact]
    public void Motion_is_switched_off_for_people_who_ask_for_less_of_it()
    {
        Assert.Matches(@"@media\s*\(prefers-reduced-motion:\s*reduce\)", Styles());
    }

    [Theory]
    [InlineData("on-surface", "surface")]
    [InlineData("on-surface", "surface-container-low")]
    [InlineData("on-surface-variant", "surface")]
    [InlineData("on-surface-variant", "surface-container-low")]
    [InlineData("outline", "surface")]
    [InlineData("outline", "surface-container-low")]
    [InlineData("primary", "surface")]
    [InlineData("primary", "surface-container-low")]
    [InlineData("on-primary", "primary")]
    public void Text_and_background_colours_meet_WCAG_AA(string text, string background)
    {
        var tokens = ColourTokens();

        var ratio = ContrastRatio(tokens[text], tokens[background]);

        Assert.True(ratio >= MinimumContrast, $"--{text} on --{background} is {ratio:F2}:1, below {MinimumContrast}:1.");
    }

    private static string Page() => File.ReadAllText(Path.Combine(RepositoryRoot(), "site", "index.html"));

    private static string Styles() => File.ReadAllText(Path.Combine(RepositoryRoot(), "site", "styles.css"));

    private static string PageText()
    {
        var body = Regex.Replace(Page(), @"<(script|style)\b.*?</\1>", " ", RegexOptions.Singleline);
        var text = WebUtility.HtmlDecode(Regex.Replace(body, "<[^>]+>", " "));

        return Regex.Replace(text, @"\s+", " ");
    }

    private static List<string> Tags(string name) =>
        Regex.Matches(Page(), $@"<{name}\b[^>]*>", RegexOptions.Singleline).Select(match => match.Value).ToList();

    private static string MetaTag(string key, string value) => FindTag("meta", key, value);

    private static string LinkTag(string rel) => FindTag("link", "rel", rel);

    private static string FindTag(string name, string key, string value) =>
        Tags(name).SingleOrDefault(tag => Attribute(tag, key) == value)
        ?? throw new Xunit.Sdk.XunitException($"<{name} {key}=\"{value}\"> is missing from the page.");

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\s{Regex.Escape(name)}\s*=\s*""(?<value>[^""]*)""");

        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
    }

    private static Dictionary<string, string> ColourTokens()
    {
        var root = Regex.Match(Styles(), @":root\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        return Regex.Matches(root, @"--(?<name>[a-z-]+)\s*:\s*#(?<hex>[0-9a-fA-F]{6})\s*;")
            .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["hex"].Value);
    }

    private static double ContrastRatio(string firstHex, string secondHex)
    {
        var first = Luminance(firstHex);
        var second = Luminance(secondHex);

        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    // WCAG 2.x relative luminance of an sRGB colour.
    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
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
