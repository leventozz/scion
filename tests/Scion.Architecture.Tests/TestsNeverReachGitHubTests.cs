using System.Text.RegularExpressions;

namespace Scion.Architecture.Tests;

/// <summary>
/// No automatic test may ask GitHub for an update. The seams are: the app under test always gets an explicit
/// SCION_UPDATE_SOURCE (a folder of releases, or <c>DesktopApp.NoUpdateSource</c>, a folder that does not exist), and the
/// update adapter's tests name a folder, or an address on this machine. This checks the test sources for the ways around that.
/// </summary>
public class TestsNeverReachGitHubTests
{
    private const string NotThisMachine = @"(?!127\.0\.0\.1|localhost)";

    private static readonly Regex[] Forbidden =
    [
        // Velopack's GitHub source asks api.github.com whatever repository it is given.
        new(@"new\s+GithubSource\s*\(", RegexOptions.Compiled),
        // The update adapter given an address that is not on this machine.
        new(@"new\s+VelopackUpdates\s*\(\s*""https?://" + NotThisMachine, RegexOptions.Compiled),
        // The app under test pointed at an address by search of the default (null) or at an address that is not on this machine.
        new(@"Environment\[""SCION_UPDATE_SOURCE""\]\s*=\s*(null|""https?://" + NotThisMachine + ")", RegexOptions.Compiled),
        // Scion's own repository, spelled out anywhere in a test.
        new(@"github\.com/leventozz", RegexOptions.Compiled),
    ];

    private static readonly string[] RuleFiles = [nameof(TestsNeverReachGitHubTests) + ".cs"];

    [Fact]
    public void No_test_source_reaches_for_github()
    {
        var offenders = TestSources.Except(RuleFiles)
            .SelectMany(file => Forbidden.Where(rule => rule.IsMatch(File.ReadAllText(file))).Select(rule => $"{file}: {rule}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_app_under_test_always_gets_an_explicit_update_source_that_is_never_the_default()
    {
        var desktopApp = TestSources.Read("Scion.Smoke.Tests", "DesktopApp.cs");

        Assert.Contains("startInfo.Environment[\"SCION_UPDATE_SOURCE\"] = updateSource ?? NoUpdateSource;", desktopApp);
        Assert.Contains("startInfo.Environment.Remove(\"SCION_UPDATE_SOURCE\");", desktopApp);
        Assert.False(Directory.Exists(NoUpdateSource), "NoUpdateSource must be a folder that does not exist.");
    }

    [Fact]
    public void No_update_source_is_a_folder_no_test_creates()
    {
        var desktopApp = TestSources.Read("Scion.Smoke.Tests", "DesktopApp.cs");

        Assert.Contains($"Path.Combine(Path.GetTempPath(), \"{Path.GetFileName(NoUpdateSource)}\")", desktopApp);
    }

    [Theory]
    [InlineData("new GithubSource(\"https://example.test/owner/repo\", null, false)")]
    [InlineData("var updates = new VelopackUpdates(\"https://github.com/owner/repo\", \"1.0.0\");")]
    [InlineData("new VelopackUpdates( \"http://example.test/repo\", \"1.0.0\")")]
    [InlineData("startInfo.Environment[\"SCION_UPDATE_SOURCE\"] = null;")]
    [InlineData("startInfo.Environment[\"SCION_UPDATE_SOURCE\"] = \"https://github.com/owner/repo\";")]
    public void The_rules_do_catch_the_ways_around_the_seams(string source) =>
        Assert.Contains(Forbidden, rule => rule.IsMatch(source));

    [Theory]
    [InlineData("new VelopackUpdates(_releases.Path, \"9.9.9\", locator)")]
    [InlineData("new VelopackUpdates(\"http://127.0.0.1:1/owner/repository\", \"9.9.9\", locator)")]
    [InlineData("new VelopackUpdates(\"http://localhost:1/owner/repository\", \"9.9.9\", locator)")]
    [InlineData("startInfo.Environment[\"SCION_UPDATE_SOURCE\"] = updateSource ?? NoUpdateSource;")]
    [InlineData("startInfo.Environment[\"SCION_UPDATE_SOURCE\"] = \"http://127.0.0.1:5000\";")]
    public void The_rules_leave_a_folder_and_this_machine_alone(string source) =>
        Assert.DoesNotContain(Forbidden, rule => rule.IsMatch(source));

    private static string NoUpdateSource => Path.Combine(Path.GetTempPath(), "scion-tests-no-update-source");
}
