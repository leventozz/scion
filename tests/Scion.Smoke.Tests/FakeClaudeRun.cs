using System.Diagnostics;
using System.Text.Json;

namespace Scion.Smoke.Tests;

/// <summary>The script, log and executable of the stand-in <c>claude</c> for one smoke test. Never the real Claude.</summary>
internal sealed class FakeClaudeRun : IDisposable
{
    private readonly TempDirectory _directory = new("scion-fake-claude");

    public FakeClaudeRun(object script)
    {
        ScriptPath = Path.Combine(_directory.Path, "script.json");
        LogPath = Path.Combine(_directory.Path, "log.jsonl");
        PidPath = Path.Combine(_directory.Path, "pid.txt");
        Write(script);
    }

    public string ScriptPath { get; }

    public string LogPath { get; }

    public string PidPath { get; }

    public string ExecutablePath { get; } = Locate();

    public void Write(object script)
    {
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["logPath"] = LogPath,
            ["pidPath"] = PidPath,
        };

        foreach (var property in JsonSerializer.SerializeToElement(script).EnumerateObject())
        {
            merged[property.Name] = property.Value;
        }

        File.WriteAllText(ScriptPath, JsonSerializer.Serialize(merged));
    }

    /// <summary>Every call the app made to the fake, in order.</summary>
    public IReadOnlyList<JsonElement> Calls() => File.Exists(LogPath)
        ? File.ReadAllLines(LogPath).Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToList()
        : [];

    /// <summary>The calls that were print-mode adaptations, with the text that was sent on standard input.</summary>
    public string[] SentPrompts() => Calls()
        .Where(call => call.GetProperty("args").EnumerateArray().Any(argument => argument.GetString() == "-p"))
        .Select(call => call.GetProperty("stdin").GetString()!)
        .ToArray();

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

    public void Dispose() => _directory.Dispose();

    /// <summary>The fake is built beside the smoke tests: tests/Scion.FakeClaude/bin/&lt;configuration&gt;/net10.0.</summary>
    private static string Locate()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var tests = output.Parent!.Parent!.Parent!.Parent!;

        return Path.Combine(tests.FullName, "Scion.FakeClaude", "bin", output.Parent.Name, output.Name, "Scion.FakeClaude.exe");
    }
}
