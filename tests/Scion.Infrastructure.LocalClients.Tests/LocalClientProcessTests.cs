using System.Text.Json;

namespace Scion.Infrastructure.LocalClients.Tests;

/// <summary>What the process runner gives a client program: the working directory it names to the program itself.</summary>
[Collection("StandIn")]
public class LocalClientProcessTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));
    private readonly StandInProcess _standIn = new();
    private readonly string? _previousScript = Environment.GetEnvironmentVariable("SCION_FAKE_CLAUDE_SCRIPT");

    public LocalClientProcessTests()
    {
        Directory.CreateDirectory(_directory);
        LogPath = Path.Combine(_directory, "log.jsonl");
        var scriptPath = Path.Combine(_directory, "script.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new { adapt = new { kind = "notJson" }, logPath = LogPath }));
        Environment.SetEnvironmentVariable("SCION_FAKE_CLAUDE_SCRIPT", scriptPath);
    }

    private string LogPath { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SCION_FAKE_CLAUDE_SCRIPT", _previousScript);
        _standIn.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task The_arguments_can_name_the_empty_working_directory_the_program_runs_in()
    {
        await LocalClientProcess.RunAsync(
            _standIn.ExecutablePath,
            workingDirectory => [.. StandInProcess.Arguments, "-C", workingDirectory],
            "",
            OutputHandling.Drop,
            CancellationToken.None);

        var call = JsonDocument.Parse(File.ReadAllLines(LogPath).Single()).RootElement;
        var arguments = call.GetProperty("args").EnumerateArray().Select(argument => argument.GetString()!).ToArray();
        var named = arguments[Array.IndexOf(arguments, "-C") + 1];

        Assert.Equal(Path.TrimEndingDirectorySeparator(named), Path.TrimEndingDirectorySeparator(call.GetProperty("cwd").GetString()!), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(0, call.GetProperty("cwdEntries").GetInt32());
    }

    [Fact]
    public async Task A_fixed_list_of_arguments_runs_in_the_same_empty_working_directory()
    {
        await LocalClientProcess.RunAsync(_standIn.ExecutablePath, StandInProcess.Arguments, "", OutputHandling.Drop, CancellationToken.None);

        var call = JsonDocument.Parse(File.ReadAllLines(LogPath).Single()).RootElement;

        Assert.Equal(0, call.GetProperty("cwdEntries").GetInt32());
        Assert.StartsWith("scion-client-", Path.GetFileName(call.GetProperty("cwd").GetString()!));
    }
}
