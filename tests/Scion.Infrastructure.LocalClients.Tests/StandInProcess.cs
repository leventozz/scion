using System.Text.Json;

namespace Scion.Infrastructure.LocalClients.Tests;

/// <summary>
/// A program that hangs and starts a child of its own, so a whole process tree can be checked. It is the stand-in
/// <c>Scion.FakeClaude</c>, built beside this project; no real client is ever called. Its script is found through
/// <c>SCION_FAKE_CLAUDE_SCRIPT</c>, which is set for as long as this object lives.
/// </summary>
internal sealed class StandInProcess : IDisposable
{
    private const string ScriptVariable = "SCION_FAKE_CLAUDE_SCRIPT";

    /// <summary>The stand-in only answers (here: hangs) to the flags a client is run with in print mode.</summary>
    public static readonly string[] Arguments = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));
    private readonly string? _previousScript = Environment.GetEnvironmentVariable(ScriptVariable);

    public StandInProcess()
    {
        Directory.CreateDirectory(_directory);
        PidPath = Path.Combine(_directory, "pid.txt");
        var scriptPath = Path.Combine(_directory, "script.json");
        File.WriteAllText(scriptPath, JsonSerializer.Serialize(new
        {
            adapt = new { kind = "hang" },
            spawnChild = true,
            spawnChildAfterMs = 400,
            pidPath = PidPath,
        }));
        Environment.SetEnvironmentVariable(ScriptVariable, scriptPath);
    }

    public string ExecutablePath { get; } = Locate();

    /// <summary>The id of the stand-in itself is written here; the id of its child goes to the same path with ".child" added.</summary>
    public string PidPath { get; }

    public int? ChildId() => ReadId(PidPath + ".child");

    /// <summary>The id of the stand-in itself, or null while its file is missing, empty or still being written.</summary>
    public int? Id() => ReadId(PidPath);

    /// <summary>
    /// The tests poll these files while the stand-in writes them. A file that is missing, not yet a number, or still open in the
    /// writer (a sharing violation) is "no id yet": the caller asks again.
    /// </summary>
    private static int? ReadId(string path)
    {
        try
        {
            return File.Exists(path) && int.TryParse(File.ReadAllText(path), out var id) ? id : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ScriptVariable, _previousScript);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>The stand-in is built beside this test project: tests/Scion.FakeClaude/bin/&lt;configuration&gt;/net10.0.</summary>
    private static string Locate()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var tests = output.Parent.Parent!.Parent!.Parent!;
        var name = OperatingSystem.IsWindows() ? "Scion.FakeClaude.exe" : "Scion.FakeClaude";

        return Path.Combine(tests.FullName, "Scion.FakeClaude", "bin", configuration, output.Name, name);
    }
}
