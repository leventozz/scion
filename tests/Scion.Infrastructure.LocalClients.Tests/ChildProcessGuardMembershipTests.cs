using System.Diagnostics;

namespace Scion.Infrastructure.LocalClients.Tests;

/// <summary>
/// Whether a process is in the guard has three answers, not two: in, not in, and "the system could not say". A failed query must
/// never be read as "not in the job", or a test (or a caller) would blame the job for what was a failed question.
/// </summary>
[Collection("StandIn")]
public class ChildProcessGuardMembershipTests : IDisposable
{
    private readonly StandInProcess _fake = new();
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started.Where(process => !HasExited(process)))
        {
            process.Kill(entireProcessTree: true);
        }

        // A process whose object was closed cannot be asked whether it has exited: it is ended by the id the stand-in wrote.
        foreach (var id in new[] { _fake.Id(), _fake.ChildId() }.OfType<int>())
        {
            try
            {
                Process.GetProcessById(id).Kill();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                // Already gone.
            }
        }

        _fake.Dispose();
    }

    [Fact]
    public void A_process_that_was_adopted_is_inside()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var guard = new ChildProcessGuard();
        var process = Start();

        Assert.True(guard.Adopt(process));

        Assert.Equal(new JobMembership.Inside(), guard.MembershipOf(process));
        Assert.True(guard.Contains(process));
    }

    [Fact]
    public void A_process_that_was_not_adopted_is_outside()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var guard = new ChildProcessGuard();
        var process = Start();

        Assert.Equal(new JobMembership.Outside(), guard.MembershipOf(process));
        Assert.False(guard.Contains(process));
    }

    [Fact]
    public void A_question_the_system_could_not_answer_is_not_read_as_outside()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var guard = new ChildProcessGuard();
        var process = Start();
        guard.Adopt(process);
        process.Close(); // the object no longer has a process: the handle cannot be asked for

        var membership = guard.MembershipOf(process);

        var failed = Assert.IsType<JobMembership.QueryFailed>(membership);
        Assert.Equal(JobMembership.InvalidHandle, failed.Win32Error);
        Assert.False(guard.Contains(process));
    }

    [Fact]
    public void Without_a_job_object_nothing_is_inside()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var guard = new ChildProcessGuard();

        Assert.Equal(new JobMembership.Outside(), guard.MembershipOf(Process.GetCurrentProcess()));
    }

    private Process Start()
    {
        var startInfo = new ProcessStartInfo(_fake.ExecutablePath, StandInProcess.Arguments)
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
}
