namespace Scion.Application.Adapting;

/// <summary>
/// What an AI provider lets the user see while it is still working. For display only: the result of an adaptation
/// is always the complete text the provider returns at the end, never the pieces reported here. A provider that cannot
/// stream reports at most one piece.
/// </summary>
public abstract record AdaptationProgress
{
    private AdaptationProgress()
    {
    }

    /// <summary>The next piece of the skill file, to be appended to what has arrived before.</summary>
    public sealed record TextArrived(string Text) : AdaptationProgress;

    /// <summary>The provider dropped what it had written and begins again (for example a retry).</summary>
    public sealed record StartedOver : AdaptationProgress;
}
