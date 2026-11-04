using System.Diagnostics;

namespace Scion.Infrastructure.LocalClients;

/// <summary>
/// Makes the system end a program and everything it starts when Scion's own process ends, however it ends: closed,
/// crashed or killed. Scion cannot run any code when it is killed, so only the operating system can do this.
/// <list type="bullet">
/// <item>Windows: a job object that kills its processes when its last handle closes. Scion holds the only handle, and the
/// system closes it when Scion's process is gone.</item>
/// <item>Other systems: nothing here. Cancelling still ends the whole process tree (<see cref="LocalClientProcess"/>), but a
/// forced end of Scion can leave a running client behind. There is no portable way to close that gap without a helper
/// process that watches Scion.</item>
/// </list>
/// </summary>
internal sealed class ChildProcessGuard : IDisposable
{
    private readonly WindowsKillOnCloseJob? _job = OperatingSystem.IsWindows() ? WindowsKillOnCloseJob.TryCreate() : null;

    /// <summary>The guard of this application: it lives as long as Scion's process, which is the point.</summary>
    public static ChildProcessGuard ForThisApplication { get; } = new();

    /// <summary>Puts the process (and so everything it starts from now on) under the guard.</summary>
    /// <returns>False when the system could not do it: the process is then ended by cancelling only.</returns>
    public bool Adopt(Process process) => OperatingSystem.IsWindows() && _job is not null && _job.Assign(process);

    /// <summary>Whether the process is under this guard: inside, outside, or the system could not say.</summary>
    public JobMembership MembershipOf(Process process) =>
        OperatingSystem.IsWindows() && _job is not null ? _job.MembershipOf(process) : new JobMembership.Outside();

    /// <summary>Whether the process is under this guard. A question the system could not answer is not a yes.</summary>
    public bool Contains(Process process) => MembershipOf(process) is JobMembership.Inside;

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            _job?.Dispose();
        }
    }
}
