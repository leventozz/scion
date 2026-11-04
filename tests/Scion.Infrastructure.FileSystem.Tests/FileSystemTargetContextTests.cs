using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem.Tests;

public class FileSystemTargetContextTests : IDisposable
{
    private readonly TempDirectory _project = new();
    private readonly TempDirectory _home = new();

    public void Dispose()
    {
        _project.Dispose();
        _home.Dispose();
    }

    private Task<TargetContext> ReadProjectAsync(Agent agent = Agent.ClaudeCode, ContextLimits? limits = null) =>
        ReadAsync(new AgentLocation(new Location.Project(_project.Path), agent), limits);

    private Task<TargetContext> ReadAsync(AgentLocation target, ContextLimits? limits = null) =>
        new FileSystemTargetContext(_home.Path, [new ClaudeCodeLayout(), new CodexLayout()])
            .ReadAsync(target, limits ?? ContextLimits.Default, CancellationToken.None);

    private static string[] Paths(TargetContext context, TargetContextFileKind kind) =>
        context.Files.Where(file => file.Kind == kind).Select(file => file.RelativePath).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Reads_the_instruction_files_of_the_agent_with_their_bytes()
    {
        _project.WriteFile("CLAUDE.md", "# Rules");
        _project.WriteFile(".claude/CLAUDE.md", "# Nested");
        _project.WriteFile("AGENTS.md", "# For Codex");

        var context = await ReadProjectAsync();

        Assert.Equal(["CLAUDE.md", ".claude/CLAUDE.md"], context.Files
            .Where(file => file.Kind == TargetContextFileKind.InstructionFile).Select(file => file.RelativePath));
        Assert.Equal("# Rules"u8.ToArray(), context.Files.First(file => file.RelativePath == "CLAUDE.md").Bytes);
    }

    [Fact]
    public async Task Reads_the_instruction_file_of_codex_for_a_codex_target()
    {
        _project.WriteFile("CLAUDE.md", "# Claude");
        _project.WriteFile("AGENTS.md", "# Codex");

        var context = await ReadProjectAsync(Agent.Codex);

        Assert.Equal(["AGENTS.md"], Paths(context, TargetContextFileKind.InstructionFile));
    }

    [Fact]
    public async Task Reads_an_instruction_file_at_most_one_byte_past_the_per_file_limit()
    {
        _project.WriteFile("CLAUDE.md", new string('x', 100));

        var context = await ReadProjectAsync(limits: new ContextLimits(10, 1000));

        Assert.Equal(11, Assert.Single(context.Files).Bytes.Length);
    }

    [Fact]
    public async Task Finds_the_manifests_of_the_allowlist_in_the_root_and_two_levels_below_it()
    {
        _project.WriteFile("App.sln", "sln");
        _project.WriteFile("App.slnx", "slnx");
        _project.WriteFile("package.json", "{}");
        _project.WriteFile("pyproject.toml", "[project]");
        _project.WriteFile("go.mod", "module x");
        _project.WriteFile("Cargo.toml", "[package]");
        _project.WriteFile("pom.xml", "<project/>");
        _project.WriteFile("build.gradle", "plugins {}");
        _project.WriteFile("src/Web/Web.csproj", "<Project/>");
        _project.WriteFile("src/Web/deep/Deep.csproj", "<Project/>");

        var context = await ReadProjectAsync();

        Assert.Equal(
            ["App.sln", "App.slnx", "Cargo.toml", "build.gradle", "go.mod", "package.json", "pom.xml", "pyproject.toml", "src/Web/Web.csproj"],
            Paths(context, TargetContextFileKind.Manifest));
    }

    [Fact]
    public async Task Never_reads_a_file_that_is_not_on_the_allowlist()
    {
        _project.WriteFile("Program.cs", "SECRET source");
        _project.WriteFile("src/Service.cs", "SECRET source");
        _project.WriteFile(".env", "SECRET=1");
        _project.WriteFile("notes.txt", "SECRET notes");
        _project.WriteFile("README.md", "SECRET readme");

        var context = await ReadProjectAsync();

        Assert.Empty(context.Files);
    }

    [Fact]
    public async Task Lists_the_names_of_the_folders_two_levels_deep_as_relative_paths()
    {
        Directory.CreateDirectory(Path.Combine(_project.Path, "src", "Web"));
        Directory.CreateDirectory(Path.Combine(_project.Path, "src", "Web", "Controllers"));
        Directory.CreateDirectory(Path.Combine(_project.Path, "tests"));

        var context = await ReadProjectAsync();

        Assert.Equal(["src", "src/Web", "tests"], context.FolderNames);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".claude")]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("Bin")]
    public async Task Skips_dot_folders_and_build_output_with_everything_in_them(string skipped)
    {
        Directory.CreateDirectory(Path.Combine(_project.Path, skipped, "inner"));
        _project.WriteFile($"{skipped}/package.json", "{}");
        _project.WriteFile($"src/{skipped}/package.json", "{}");
        Directory.CreateDirectory(Path.Combine(_project.Path, "src", skipped, "inner"));

        var context = await ReadProjectAsync();

        Assert.Equal(["src"], context.FolderNames);
        Assert.Empty(context.Files);
    }

    [Fact]
    public async Task Does_not_follow_a_link_to_a_folder()
    {
        using var elsewhere = new TempDirectory();
        elsewhere.WriteFile("package.json", "{}");
        Directory.CreateDirectory(Path.Combine(elsewhere.Path, "private-folder"));
        _project.LinkDirectory("linked", elsewhere.Path);

        var context = await ReadProjectAsync();

        Assert.Empty(context.FolderNames);
        Assert.Empty(context.Files);
    }

    [Fact]
    public async Task A_project_folder_that_is_gone_gives_an_empty_context()
    {
        var context = await ReadAsync(new AgentLocation(new Location.Project(Path.Combine(_project.Path, "gone")), Agent.ClaudeCode));

        Assert.Empty(context.Files);
        Assert.Empty(context.FolderNames);
    }

    [Fact]
    public async Task A_personal_target_reads_only_its_instruction_file()
    {
        _home.WriteFile(".claude/CLAUDE.md", "# Mine");
        _home.WriteFile("package.json", "{}");
        _home.WriteFile("Documents/App.csproj", "<Project/>");
        Directory.CreateDirectory(Path.Combine(_home.Path, "Pictures"));

        var context = await ReadAsync(new AgentLocation(new Location.Personal(), Agent.ClaudeCode));

        Assert.Equal([".claude/CLAUDE.md"], context.Files.Select(file => file.RelativePath));
        Assert.Empty(context.FolderNames);
    }

    [Fact]
    public async Task Reads_text_exactly_as_it_is_with_a_BOM()
    {
        _project.WriteFile("package.json", "{}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var context = await ReadProjectAsync();

        Assert.Equal((byte[])[0xEF, 0xBB, 0xBF, .. "{}"u8], Assert.Single(context.Files).Bytes);
    }
}
