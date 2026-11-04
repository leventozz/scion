namespace Scion.Infrastructure.LocalClients;

/// <summary>
/// Whether a process is in a job: yes, no, or the system could not say. A failed question is its own answer, so it is never
/// mistaken for "not in the job".
/// </summary>
internal abstract record JobMembership
{
    /// <summary>The Win32 code for a handle that cannot be used (also: a process object that has no process any more).</summary>
    public const int InvalidHandle = 6;

    private JobMembership()
    {
    }

    public sealed record Inside : JobMembership;

    public sealed record Outside : JobMembership;

    /// <param name="Win32Error">What the system said (<see cref="InvalidHandle"/> when the process object has no process).</param>
    public sealed record QueryFailed(int Win32Error) : JobMembership;
}
