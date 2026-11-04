using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Scion.Infrastructure.LocalClients;

/// <summary>A Windows job object whose processes are ended by the system when the last handle to the job is closed.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsKillOnCloseJob : IDisposable
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private readonly SafeFileHandle _handle;

    private WindowsKillOnCloseJob(SafeFileHandle handle) => _handle = handle;

    /// <returns>The job, or null when the system would not make one.</returns>
    public static WindowsKillOnCloseJob? TryCreate()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            return null;
        }

        var limits = new ExtendedLimits { BasicLimits = new BasicLimits { LimitFlags = KillOnJobClose } };
        if (!SetInformationJobObject(handle, ExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()))
        {
            handle.Dispose();

            return null;
        }

        return new WindowsKillOnCloseJob(handle);
    }

    /// <returns>False when the process cannot be put in the job (for example it is in another job that does not allow it).</returns>
    public bool Assign(Process process)
    {
        try
        {
            return AssignProcessToJobObject(_handle, process.Handle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public JobMembership MembershipOf(Process process)
    {
        try
        {
            var asked = IsProcessInJob(process.Handle, _handle, out var inJob);

            return !asked
                ? new JobMembership.QueryFailed(Marshal.GetLastWin32Error())
                : inJob ? new JobMembership.Inside() : new JobMembership.Outside();
        }
        catch (InvalidOperationException)
        {
            // A process object that was closed (or whose process is gone) has no handle to ask with.
            return new JobMembership.QueryFailed(JobMembership.InvalidHandle);
        }
        catch (Win32Exception exception)
        {
            return new JobMembership.QueryFailed(exception.NativeErrorCode);
        }
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimits information, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits BasicLimits;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
