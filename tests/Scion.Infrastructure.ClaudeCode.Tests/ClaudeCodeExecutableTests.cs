namespace Scion.Infrastructure.ClaudeCode.Tests;

/// <summary>What is Claude Code's own: the program name and the folders its installers use. The search itself is tested in LocalClients.</summary>
public class ClaudeCodeExecutableTests : IDisposable
{
    private static readonly string BinaryName = OperatingSystem.IsWindows() ? "claude.exe" : "claude";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));

    public ClaudeCodeExecutableTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");

        return path;
    }

    private ClaudeCodeExecutable Executable(string? explicitPath = null, string? searchPath = null) =>
        new(explicitPath, Path.Combine(_root, "home"), name => name switch
        {
            "PATH" => searchPath,
            "PATHEXT" => ".COM;.EXE;.BAT;.CMD",
            _ => null,
        });

    [Fact]
    public void Uses_the_explicit_path_when_it_exists_even_if_another_claude_is_on_the_path()
    {
        var explicitPath = Touch("fake/fake-claude");
        Touch($"bin/{BinaryName}");

        var found = Executable(explicitPath, Path.Combine(_root, "bin")).Locate();

        Assert.Equal(explicitPath, found);
    }

    [Fact]
    public void Finds_claude_in_a_folder_of_the_search_path()
    {
        var expected = Touch($"tools/{BinaryName}");

        var found = Executable(searchPath: string.Join(Path.PathSeparator, Path.Combine(_root, "empty"), Path.Combine(_root, "tools"))).Locate();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Looks_in_the_local_bin_folder_of_the_home_directory_when_it_is_not_on_the_path()
    {
        var expected = Touch($"home/.local/bin/{BinaryName}");

        var found = Executable(searchPath: Path.Combine(_root, "empty")).Locate();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Prefers_the_search_path_over_the_known_locations()
    {
        Touch($"home/.local/bin/{BinaryName}");
        var onPath = Touch($"tools/{BinaryName}");

        var found = Executable(searchPath: Path.Combine(_root, "tools")).Locate();

        Assert.Equal(onPath, found);
    }

    [Fact]
    public void Gives_null_when_claude_is_nowhere()
    {
        Assert.Null(Executable(searchPath: Path.Combine(_root, "empty")).Locate());
    }

}
