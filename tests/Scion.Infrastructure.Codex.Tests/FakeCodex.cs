using System.Diagnostics;
using System.Text.Json;

namespace Scion.Infrastructure.Codex.Tests;

/// <summary>One run of the stand-in <c>codex</c>: its script, its log and its executable. Never the real Codex.</summary>
internal sealed class FakeCodex : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));

    public FakeCodex(object? script = null)
    {
        Directory.CreateDirectory(_directory);
        ScriptPath = Path.Combine(_directory, "script.json");
        LogPath = Path.Combine(_directory, "log.jsonl");
        PidPath = Path.Combine(_directory, "pid.txt");
        Write(script ?? new { });
    }

    public string ScriptPath { get; }

    public string LogPath { get; }

    public string PidPath { get; }

    /// <summary>The path to give the code under test as the executable (SCION_CODEX_PATH).</summary>
    public string ExecutablePath { get; } = Locate();

    /// <summary>Environment of the code under test: it must find the script, as it would find any variable.</summary>
    public IDisposable UseAsEnvironment() => new EnvironmentVariable("SCION_FAKE_CODEX_SCRIPT", ScriptPath);

    public void Write(object script)
    {
        var withPaths = JsonSerializer.SerializeToElement(script);
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["logPath"] = LogPath,
            ["pidPath"] = PidPath,
        };

        foreach (var property in withPaths.EnumerateObject())
        {
            merged[property.Name] = property.Value;
        }

        File.WriteAllText(ScriptPath, JsonSerializer.Serialize(merged));
    }

    public IReadOnlyList<Call> Calls() => File.Exists(LogPath)
        ? File.ReadAllLines(LogPath).Select(line => JsonSerializer.Deserialize<Call>(line, JsonOptions)!).ToList()
        : [];

    public bool IsRunning()
    {
        if (!File.Exists(PidPath))
        {
            return false;
        }

        try
        {
            return !Process.GetProcessById(int.Parse(File.ReadAllText(PidPath))).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async Task WaitUntilStartedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(LogPath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>The fake is built beside this test project: tests/Scion.FakeCodex/bin/&lt;configuration&gt;/net10.0.</summary>
    private static string Locate()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var tests = output.Parent.Parent!.Parent!.Parent!;
        var name = OperatingSystem.IsWindows() ? "Scion.FakeCodex.exe" : "Scion.FakeCodex";

        return Path.Combine(tests.FullName, "Scion.FakeCodex", "bin", configuration, output.Name, name);
    }

    internal sealed record Call(
        string[] Args,
        string Stdin,
        string Cwd,
        int CwdEntries,
        string? WorkDirectory,
        string? SchemaPath,
        string? Schema,
        bool ApiKeySet);

    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariable(string name, string value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
