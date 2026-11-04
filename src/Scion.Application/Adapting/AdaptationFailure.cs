namespace Scion.Application.Adapting;

/// <summary>Why an adaptation produced no skill. A closed set: the UI has a message for each.</summary>
public abstract record AdaptationFailure
{
    private AdaptationFailure()
    {
    }

    public sealed record NotInstalled : AdaptationFailure;

    public sealed record SignedOut : AdaptationFailure;

    public sealed record Cancelled : AdaptationFailure;

    public sealed record TimedOut : AdaptationFailure;

    public sealed record UsageLimitReached : AdaptationFailure;

    /// <param name="Message">What the provider's client said, as far as it could be told.</param>
    public sealed record Failed(string Message) : AdaptationFailure;
}
