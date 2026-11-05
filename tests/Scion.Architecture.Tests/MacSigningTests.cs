using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scion.Architecture.Tests;

/// <summary>
/// How the Mac packages are signed and notarized, as rules on the files that do it (scripts, workflows, the entitlements). None of it
/// can run on a test machine: it needs a Mac, a Developer ID certificate and an App Store Connect key. What these tests can say is
/// that no secret is written down, logged or left behind, and that the package asks the system for no more than it needs.
/// </summary>
public class MacSigningTests
{
    private const string PackScript = "tools/macos/pack.sh";
    private const string KeychainScript = "tools/macos/signing-keychain.sh";

    public static TheoryData<string> Workflows => new()
    {
        ".github/workflows/release.yml",
        ".github/workflows/package-macos.yml",
    };

    [Fact]
    public void The_entitlements_grant_only_what_the_dotnet_runtime_needs_to_run_its_compiled_code()
    {
        var entitlements = Entitlements();

        Assert.Equal(["com.apple.security.cs.allow-jit"], entitlements);
    }

    [Fact]
    public void The_pack_script_signs_with_the_entitlements_notarizes_and_makes_the_zip_only_package()
    {
        var script = TestSources.ReadRepositoryFile(PackScript);

        Assert.Contains("vpk pack", script);
        Assert.Contains("--signAppIdentity", script);
        Assert.Contains("--signEntitlements tools/macos/Scion.entitlements", script);
        Assert.Contains("--notaryProfile", script);
        Assert.Contains("--keychain", script);
        Assert.Contains("--noInst", script);
        Assert.Contains("--channel", script);
        Assert.Contains("--icon src/Scion.Host.Desktop/Assets/scion.icns", script);
    }

    [Fact]
    public void The_pack_script_checks_the_app_inside_the_zip_it_ships()
    {
        var script = TestSources.ReadRepositoryFile(PackScript);

        Assert.Contains("codesign --verify --deep --strict", script);
        Assert.Contains("xcrun stapler validate", script);
        Assert.Contains("-Portable.zip", script);
    }

    [Fact]
    public void The_icon_is_an_icns_file_with_the_sizes_a_Mac_asks_for()
    {
        var bytes = File.ReadAllBytes(TestSources.RepositoryFilePath("src/Scion.Host.Desktop/Assets/scion.icns"));

        Assert.Equal("icns", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(4, 4)));
        Assert.Contains("ic10", EntryTypes(bytes));
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void The_step_that_deletes_the_signing_keychain_always_runs(string workflow)
    {
        var deleting = Steps(workflow).Where(step => step.Contains($"{KeychainScript} delete")).ToList();

        Assert.Single(deleting);
        Assert.Contains("if: always()", deleting[0]);
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void The_signing_keychain_is_created_before_the_package_is_made_and_deleted_after(string workflow)
    {
        var steps = Steps(workflow).ToList();
        var create = steps.FindIndex(step => step.Contains($"{KeychainScript} create"));
        var pack = steps.FindIndex(step => step.Contains(PackScript));
        var delete = steps.FindIndex(step => step.Contains($"{KeychainScript} delete"));

        Assert.True(create >= 0 && create < pack && pack < delete, $"create {create}, pack {pack}, delete {delete}");
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void A_signing_secret_enters_a_workflow_only_as_an_environment_variable(string workflow)
    {
        var lines = TestSources.ReadRepositoryFile(workflow).Split('\n')
            .Where(line => Regex.IsMatch(line, @"secrets\.(MACOS|APPLE)_"));

        Assert.All(lines, line => Assert.Matches(@"^\s+[A-Z][A-Z0-9_]*:\s+\$\{\{ secrets\.(MACOS|APPLE)_[A-Z0-9_]+ \}\}\s*$", line));
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void The_Mac_jobs_of_a_workflow_never_trace_their_commands(string workflow)
    {
        Assert.DoesNotMatch(Tracing, TestSources.ReadRepositoryFile(workflow));
    }

    [Theory]
    [InlineData(PackScript)]
    [InlineData(KeychainScript)]
    [InlineData("tools/macos/read-versions.sh")]
    public void No_Mac_script_traces_its_commands_or_prints_a_secret(string script)
    {
        var source = TestSources.ReadRepositoryFile(script);

        Assert.DoesNotMatch(Tracing, source);
        Assert.DoesNotMatch(@"\b(echo|printf)\b[^\n]*\$\{?(MACOS_|APPLE_|KEYCHAIN_PASSWORD)", source);
    }

    [Fact]
    public void The_keychain_script_keeps_what_it_writes_private_and_lets_only_the_signing_tools_use_the_key()
    {
        var script = TestSources.ReadRepositoryFile(KeychainScript);
        var imports = script.Split('\n').Where(line => line.Contains("security import")).ToList();

        Assert.Contains("umask 077", script);
        Assert.Contains("::add-mask::", script);
        Assert.Contains("security set-key-partition-list -S apple-tool:,apple:", script);
        Assert.NotEmpty(imports);
        Assert.All(imports, line => Assert.Contains("-T /usr/bin/codesign", line));
        Assert.All(imports, line => Assert.DoesNotMatch(@"\s-A(\s|$)", line));
    }

    [Fact]
    public void The_keychain_script_makes_the_password_each_time_and_takes_the_one_Developer_ID_identity_it_finds()
    {
        var script = TestSources.ReadRepositoryFile(KeychainScript);

        Assert.Contains("openssl rand", script);
        Assert.Contains("security find-identity -v -p codesigning", script);
        Assert.Contains("Developer ID Application", script);
        Assert.Contains("notarytool store-credentials scion-notary", script);
        Assert.Contains("security delete-keychain", script);
    }

    [Fact]
    public void The_release_adds_the_Mac_packages_to_the_release_the_Windows_job_published_through_vpk()
    {
        var release = TestSources.ReadRepositoryFile(".github/workflows/release.yml");
        var mac = release[release.IndexOf("  macos:", StringComparison.Ordinal)..];

        Assert.Contains("vpk upload github", mac);
        Assert.Contains("--merge", mac);
        Assert.Contains("--channel", mac);
        Assert.DoesNotContain("gh release upload", mac);
        Assert.Contains("Scion-osx-arm64-Portable.zip", mac);
        Assert.Contains("Scion-osx-x64-Portable.zip", mac);
    }

    private static readonly Regex Tracing = new(@"(?m)(\bset\s+-[a-zA-Z]*x|xtrace|\bbash\s+-[a-zA-Z]*x\b)", RegexOptions.Compiled);

    /// <summary>The steps of a job, without their comments: in these workflows each starts with a dash at six spaces.</summary>
    private static IEnumerable<string> Steps(string workflow) =>
        Regex.Split(Regex.Replace(TestSources.ReadRepositoryFile(workflow), @"(?m)^[ \t]*#[^\n]*\n", ""), @"(?m)^      - ").Skip(1);

    private static IEnumerable<string> Entitlements() =>
        XDocument.Parse(TestSources.ReadRepositoryFile("tools/macos/Scion.entitlements"))
            .Descendants("dict").Single().Elements()
            .Where(element => element.Name == "key"
                && element.ElementsAfterSelf().First().Name == "true")
            .Select(element => element.Value);

    private static IEnumerable<string> EntryTypes(byte[] icns)
    {
        var position = 8;
        while (position + 8 <= icns.Length)
        {
            yield return System.Text.Encoding.ASCII.GetString(icns, position, 4);
            position += System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(icns.AsSpan(position + 4, 4));
        }
    }
}
