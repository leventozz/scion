using System.Text.RegularExpressions;

namespace Scion.Architecture.Tests;

/// <summary>
/// No automatic test may start the user's real Claude Code or Codex. The seams are: the app under test always gets an
/// explicit SCION_CLAUDE_PATH and SCION_CODEX_PATH (a stand-in, or <c>DesktopApp.NoClaude</c> / <c>DesktopApp.NoCodex</c>, a
/// file that does not exist), and the infrastructure tests always name their executable. This checks the test sources for
/// the ways around that.
/// </summary>
public class TestsNeverCallARealAiClientTests
{
    private static readonly Regex[] Forbidden =
    [
        // An executable without an explicit path searches PATH and the installer folders: the real one.
        new(@"new\s+ClaudeCodeExecutable\s*\(\s*(null|default|string\.Empty|"""")\s*,", RegexOptions.Compiled),
        // A local-client executable for "claude" built by hand skips the adapter's explicit path (and its tests' stand-in).
        new(@"new\s+LocalClientExecutable\s*\(\s*""claude", RegexOptions.Compiled),
        // Starting a program called "claude" by name.
        new(@"(ProcessStartInfo|Process\.Start)\s*\(\s*""claude(\.exe|\.cmd)?""", RegexOptions.Compiled),
        // The app under test pointed at a claude by search instead of by path.
        new(@"Environment\[""SCION_CLAUDE_PATH""\]\s*=\s*(null|""claude)", RegexOptions.Compiled),

        // The same four ways for Codex.
        new(@"new\s+CodexExecutable\s*\(\s*(null|default|string\.Empty|"""")\s*,", RegexOptions.Compiled),
        new(@"new\s+LocalClientExecutable\s*\(\s*""codex", RegexOptions.Compiled),
        new(@"(ProcessStartInfo|Process\.Start)\s*\(\s*""codex(\.exe|\.cmd)?""", RegexOptions.Compiled),
        new(@"Environment\[""SCION_CODEX_PATH""\]\s*=\s*(null|""codex)", RegexOptions.Compiled),
    ];

    [Fact]
    public void No_test_source_locates_a_real_ai_client()
    {
        var offenders = TestSources()
            .SelectMany(file => Forbidden.Where(rule => rule.IsMatch(File.ReadAllText(file))).Select(rule => $"{file}: {rule}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_app_under_test_always_gets_an_explicit_claude_that_is_never_found_by_searching()
    {
        var desktopApp = File.ReadAllText(Path.Combine(TestsRoot(), "Scion.Smoke.Tests", "DesktopApp.cs"));

        Assert.Contains("startInfo.Environment[\"SCION_CLAUDE_PATH\"] = claudePath ?? NoClaude;", desktopApp);
        Assert.Contains("startInfo.Environment.Remove(\"SCION_CLAUDE_PATH\");", desktopApp);
        Assert.False(File.Exists(Smoke("claude")), "NoClaude must be a file that does not exist.");
    }

    [Fact]
    public void The_app_under_test_always_gets_an_explicit_codex_that_is_never_found_by_searching()
    {
        var desktopApp = File.ReadAllText(Path.Combine(TestsRoot(), "Scion.Smoke.Tests", "DesktopApp.cs"));

        Assert.Contains("startInfo.Environment[\"SCION_CODEX_PATH\"] = codexPath ?? NoCodex;", desktopApp);
        Assert.Contains("startInfo.Environment.Remove(\"SCION_CODEX_PATH\");", desktopApp);
        Assert.False(File.Exists(Smoke("codex")), "NoCodex must be a file that does not exist.");
    }

    [Theory]
    [InlineData("var e = new ClaudeCodeExecutable(null, home, env);")]
    [InlineData("var e = new ClaudeCodeExecutable( \"\" , home, env);")]
    [InlineData("var e = new LocalClientExecutable(\"claude\", null, folders, env);")]
    [InlineData("new ProcessStartInfo(\"claude\")")]
    [InlineData("Process.Start(\"claude.exe\", args)")]
    [InlineData("startInfo.Environment[\"SCION_CLAUDE_PATH\"] = null;")]
    [InlineData("var e = new CodexExecutable(null, home, env);")]
    [InlineData("var e = new CodexExecutable( \"\" , home, env);")]
    [InlineData("var e = new LocalClientExecutable(\"codex\", null, folders, env);")]
    [InlineData("new ProcessStartInfo(\"codex\")")]
    [InlineData("Process.Start(\"codex.exe\", args)")]
    [InlineData("startInfo.Environment[\"SCION_CODEX_PATH\"] = null;")]
    public void The_rules_do_catch_the_ways_around_the_seams(string source) =>
        Assert.Contains(Forbidden, rule => rule.IsMatch(source));

    [Theory]
    [InlineData("new ClaudeCodeExecutable(executablePath ?? _fake.ExecutablePath, \"/unused\", _ => null)")]
    [InlineData("new LocalClientExecutable(\"some-client\", explicitPath, [knownFolder], _ => null)")]
    [InlineData("startInfo.Environment[\"SCION_CLAUDE_PATH\"] = claudePath ?? NoClaude;")]
    [InlineData("new CodexExecutable(executablePath ?? _fake.ExecutablePath, \"/unused\", _ => null)")]
    [InlineData("startInfo.Environment[\"SCION_CODEX_PATH\"] = codexPath ?? NoCodex;")]
    public void The_rules_leave_the_stand_in_alone(string source) =>
        Assert.DoesNotContain(Forbidden, rule => rule.IsMatch(source));

    private static string Smoke(string client) => Path.Combine(Path.GetTempPath(), $"scion-tests-no-{client}", $"{client}.exe");

    private static IEnumerable<string> TestSources() =>
        Directory.EnumerateFiles(TestsRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && Path.GetFileName(file) != nameof(TestsNeverCallARealAiClientTests) + ".cs");

    private static string TestsRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scion.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("scion.slnx not found."), "tests");
    }
}
