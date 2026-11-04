namespace Scion.Application.Updating;

/// <summary>What looking for a newer Scion found. A closed set: the UI has a line for each.</summary>
public abstract record UpdateCheck
{
    private UpdateCheck()
    {
    }

    /// <summary>Nothing was asked: the check was not wanted (the user switched it off for the start of Scion).</summary>
    public sealed record Skipped : UpdateCheck;

    /// <summary>This Scion was not installed from a release (for example started from source), so it has nothing to update.</summary>
    public sealed record RunningFromSource : UpdateCheck;

    public sealed record UpToDate(string CurrentVersion) : UpdateCheck;

    public sealed record Available(AvailableUpdate Update) : UpdateCheck;
}
