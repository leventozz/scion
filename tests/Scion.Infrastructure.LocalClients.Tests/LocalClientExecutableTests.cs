namespace Scion.Infrastructure.LocalClients.Tests;

public class LocalClientExecutableTests : IDisposable
{
    private static readonly string BinaryName = OperatingSystem.IsWindows() ? "some-client.exe" : "some-client";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));

    public LocalClientExecutableTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");

        return path;
    }

    private LocalClientExecutable Executable(string? explicitPath = null, string? searchPath = null) =>
        new("some-client", explicitPath, [Path.Combine(_root, "known", "bin"), Path.Combine(_root, "other", "bin")], name => name switch
        {
            "PATH" => searchPath,
            "PATHEXT" => ".COM;.EXE;.BAT;.CMD",
            _ => null,
        });

    [Fact]
    public void Uses_the_explicit_path_when_it_exists_even_if_another_client_is_on_the_path()
    {
        var explicitPath = Touch("fake/fake-client");
        Touch($"bin/{BinaryName}");

        var found = Executable(explicitPath, Path.Combine(_root, "bin")).Locate();

        Assert.Equal(explicitPath, found);
    }

    [Fact]
    public void An_explicit_path_that_does_not_exist_is_not_installed_and_does_not_fall_back_to_the_path()
    {
        Touch($"bin/{BinaryName}");

        var found = Executable(Path.Combine(_root, "nowhere", "some-client"), Path.Combine(_root, "bin")).Locate();

        Assert.Null(found);
    }

    [Fact]
    public void Finds_the_client_in_a_folder_of_the_search_path()
    {
        var expected = Touch($"tools/{BinaryName}");

        var found = Executable(searchPath: string.Join(Path.PathSeparator, Path.Combine(_root, "empty"), Path.Combine(_root, "tools"))).Locate();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Looks_for_the_program_name_it_was_given_and_no_other()
    {
        Touch($"tools/{(OperatingSystem.IsWindows() ? "other-client.exe" : "other-client")}");

        Assert.Null(Executable(searchPath: Path.Combine(_root, "tools")).Locate());
    }

    [Fact]
    public void Prefers_the_exe_over_the_cmd_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Touch("tools/some-client.cmd");
        var exe = Touch("tools/some-client.exe");

        var found = Executable(searchPath: Path.Combine(_root, "tools")).Locate();

        Assert.Equal(exe, found);
    }

    [Fact]
    public void Takes_the_cmd_when_there_is_no_exe_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var cmd = Touch("tools/some-client.cmd");

        var found = Executable(searchPath: Path.Combine(_root, "tools")).Locate();

        Assert.Equal(cmd, found);
    }

    [Fact]
    public void Looks_in_the_known_folders_when_it_is_not_on_the_path()
    {
        var expected = Touch($"known/bin/{BinaryName}");

        var found = Executable(searchPath: Path.Combine(_root, "empty")).Locate();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Looks_in_the_known_folders_in_the_order_given()
    {
        var first = Touch($"known/bin/{BinaryName}");
        Touch($"other/bin/{BinaryName}");

        Assert.Equal(first, Executable(searchPath: Path.Combine(_root, "empty")).Locate());
    }

    [Fact]
    public void Prefers_the_search_path_over_the_known_folders()
    {
        Touch($"known/bin/{BinaryName}");
        var onPath = Touch($"tools/{BinaryName}");

        var found = Executable(searchPath: Path.Combine(_root, "tools")).Locate();

        Assert.Equal(onPath, found);
    }

    [Fact]
    public void Gives_null_when_the_client_is_nowhere()
    {
        Assert.Null(Executable(searchPath: Path.Combine(_root, "empty")).Locate());
    }

    [Fact]
    public void Gives_null_when_there_is_no_search_path_at_all()
    {
        Assert.Null(Executable(searchPath: null).Locate());
    }
}
