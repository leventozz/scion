using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace Scion.Infrastructure.LocalClients.Tests;

/// <summary>
/// A client process and everything it starts must end when Scion ends, however Scion ends. On Windows the system does
/// it (a job object that kills its processes when its last handle closes: the handle closes when Scion's process is gone,
/// forced or not). Closing the guard here is that same event. The stand-in starts a child of its own, so a whole tree is
/// checked. Other systems have no such mechanism in this code: see CLAUDE.md.
/// <para>
/// A test of processes can fail for reasons that have nothing to do with the code (a slow start, a late write of a file). So
/// every step is written to the test output with the time since the test began, every assert says which one it is and shows a
/// snapshot of the processes (id, name, start time, exit state, job membership) and the raw content of the id files, and a
/// failed question to the system is told apart from "not in the job" (see <see cref="JobMembership"/>).
/// </para>
/// </summary>
[Collection("StandIn")]
public class ChildProcessGuardTests : IDisposable
{
    private static readonly string[] PrintArguments = StandInProcess.Arguments;

    private readonly StandInProcess _fake = new();
    private readonly List<Process> _started = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ITestOutputHelper _output;

    public ChildProcessGuardTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        Log("cleaning up");
        foreach (var process in _started.Where(process => !HasExited(process)))
        {
            process.Kill(entireProcessTree: true);
        }

        if (_fake.ChildId() is { } child && IsAlive(child))
        {
            Process.GetProcessById(child).Kill();
        }

        _fake.Dispose();
    }

    [Fact]
    public async Task Closing_the_guard_ends_the_process_and_the_children_it_started()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var guard = new ChildProcessGuard();
        var process = StartFake();
        Log("the stand-in was started");

        Assert.True(guard.Adopt(process), Describe("adopting the stand-in into the guard", guard, process));
        Log("the stand-in was adopted");
        var child = await WaitForChildAsync();
        Log($"the child {child} was started");

        Assert.True(IsAlive(child), Describe("the child must be alive before the guard is closed", guard, process, child));
        Assert.False(HasExited(process), Describe("the stand-in must be running before the guard is closed", guard, process, child));
        guard.Dispose();
        Log("the guard was closed");

        await WaitUntilAsync(() => HasExited(process) && !IsAlive(child));
        Assert.True(HasExited(process), Describe("the stand-in must have ended once the guard was closed", null, process, child));
        Assert.False(IsAlive(child), Describe("the child must have ended once the guard was closed", null, process, child));
    }

    [Fact]
    public async Task Without_the_guard_the_child_outlives_a_parent_that_is_ended_which_is_what_the_guard_is_for()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var process = StartFake();
        var child = await WaitForChildAsync();
        Log($"the child {child} was started");

        process.Kill();
        await process.WaitForExitAsync();
        Log("the stand-in was ended");
        await Task.Delay(500);

        Assert.True(IsAlive(child), Describe("the child must outlive a parent that no guard looks after", null, process, child));
    }

    [Fact]
    public async Task Every_client_the_application_runs_is_in_the_guard_of_the_application()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        var run = LocalClientProcess.RunAsync(_fake.ExecutablePath, PrintArguments, "", OutputHandling.Drop, cancellation.Token);
        Log("the run was started");
        var child = await WaitForChildAsync();
        Log($"the child {child} was started");

        var clientId = await ReadClientIdAsync();
        using var client = Process.GetProcessById(clientId);
        Assert.True(
            ChildProcessGuard.ForThisApplication.Contains(client),
            Describe("the client of a run must be in the guard of the application", ChildProcessGuard.ForThisApplication, client, child));
        using var grandchild = Process.GetProcessById(child);
        Assert.True(
            ChildProcessGuard.ForThisApplication.Contains(grandchild),
            Describe("what the client started must be in the guard of the application too", ChildProcessGuard.ForThisApplication, client, child));

        await cancellation.CancelAsync();
        Log("the run was cancelled");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task Cancelling_a_run_ends_the_children_too()
    {
        using var cancellation = new CancellationTokenSource();
        var run = LocalClientProcess.RunAsync(_fake.ExecutablePath, PrintArguments, "", OutputHandling.Drop, cancellation.Token);
        Log("the run was started");
        var child = await WaitForChildAsync();
        Log($"the child {child} was started");

        await cancellation.CancelAsync();
        Log("the run was cancelled");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        await WaitUntilAsync(() => !IsAlive(child));
        Assert.False(IsAlive(child), Describe("the child must have ended once the run was cancelled", null, null, child));
    }

    private Process StartFake()
    {
        var startInfo = new ProcessStartInfo(_fake.ExecutablePath, PrintArguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var process = Process.Start(startInfo)!;
        process.StandardInput.Close();
        _started.Add(process);

        return process;
    }

    private async Task<int> WaitForChildAsync()
    {
        await WaitUntilAsync(() => _fake.ChildId() is not null, TimeSpan.FromSeconds(15));

        return _fake.ChildId() ?? throw new InvalidOperationException(
            $"The stand-in did not write the id of its child within 15 s. {Files()} Elapsed: {_clock.Elapsed.TotalSeconds:0.00} s.");
    }

    /// <summary>The id the stand-in wrote for itself; a file that is missing or not (yet) a number says what it holds.</summary>
    private async Task<int> ReadClientIdAsync()
    {
        await WaitUntilAsync(() => _fake.Id() is not null);

        return _fake.Id() ?? throw new InvalidOperationException(
            $"The id file of the stand-in does not hold a number. {Files()} Elapsed: {_clock.Elapsed.TotalSeconds:0.00} s.");
    }

    private void Log(string step) => _output.WriteLine($"[{_clock.Elapsed.TotalSeconds,6:0.000} s] {step}");

    /// <summary>The message of an assert: what was expected, then what the processes look like at that moment.</summary>
    private string Describe(string expectation, ChildProcessGuard? guard, Process? process, int? child = null)
    {
        var text = new StringBuilder()
            .AppendLine($"Expected: {expectation}.")
            .AppendLine($"Elapsed since the test began: {_clock.Elapsed.TotalSeconds:0.000} s.")
            .AppendLine(Files());

        if (process is not null)
        {
            text.AppendLine(Snapshot("stand-in", process, guard));
        }

        if (child is { } childId)
        {
            text.AppendLine(SnapshotOfId("child", childId, guard));
        }

        return text.ToString();
    }

    private string Files() =>
        $"Id file: {Raw(_fake.PidPath)}; child id file: {Raw(_fake.PidPath + ".child")} "
        + $"(child file written {WrittenAt(_fake.PidPath + ".child")}).";

    private static string Raw(string path)
    {
        try
        {
            return File.Exists(path) ? $"\"{File.ReadAllText(path)}\"" : "missing";
        }
        catch (IOException exception)
        {
            return $"unreadable ({exception.Message})";
        }
    }

    private string WrittenAt(string path) =>
        File.Exists(path) ? $"{(File.GetLastWriteTimeUtc(path) - DateTime.UtcNow).TotalSeconds:0.00} s from now" : "never";

    private static string Snapshot(string name, Process process, ChildProcessGuard? guard)
    {
        try
        {
            var exited = process.HasExited;
            var started = process.StartTime.ToUniversalTime().ToString("HH:mm:ss.fff");
            var state = exited ? $"exited with code {process.ExitCode}" : "running";

            return $"{name}: id {process.Id}, {process.ProcessName}, started {started} UTC, {state}, {MembershipText(process, guard)}";
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"{name}: the process could not be described ({exception.GetType().Name}: {exception.Message})";
        }
    }

    private static string SnapshotOfId(string name, int id, ChildProcessGuard? guard)
    {
        try
        {
            using var process = Process.GetProcessById(id);

            return Snapshot(name, process, guard);
        }
        catch (ArgumentException)
        {
            return $"{name}: id {id}, no such process (it has ended)";
        }
    }

    private static string MembershipText(Process process, ChildProcessGuard? guard) =>
        guard is null
            ? "job membership not asked"
            : guard.MembershipOf(process) switch
            {
                JobMembership.Inside => "inside the job",
                JobMembership.Outside => "outside the job",
                JobMembership.QueryFailed failed => $"job membership unknown: the system said Win32 error {failed.Win32Error}",
                _ => "job membership unknown",
            };

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool IsAlive(int id)
    {
        try
        {
            return !Process.GetProcessById(id).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
