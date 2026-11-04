namespace Scion.Application.Adapting;

/// <summary>
/// Something that would have been shared but is not, and why. Carried by the request next to the shared items so the
/// user can see it; it is never part of what is sent.
/// </summary>
/// <param name="Label">The label the item would have had among the shared items.</param>
public sealed record OmittedItem(string Label, OmittedReason Reason);

public abstract record OmittedReason
{
    private OmittedReason()
    {
    }

    /// <summary>The item is bigger than the limit for one file (<paramref name="LimitBytes"/>).</summary>
    public sealed record OverFileLimit(int LimitBytes) : OmittedReason;

    /// <summary>The item is within the limit for one file, but the total limit was already reached.</summary>
    public sealed record TotalLimitReached : OmittedReason;
}
