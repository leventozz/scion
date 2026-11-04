namespace Scion.Application.Adapting;

/// <summary>
/// The one line a provider has for each way of paying for it. A basis without a line shows nothing. <paramref name="Unknown"/>
/// is the line for a client that does not say how it is signed in.
/// </summary>
public sealed record AiUsageNotes(string? Subscription, string? ApiKey, string? Unknown = null)
{
    /// <summary>For a provider whose cost cannot be told from its client: the same line whatever the client reports.</summary>
    public static AiUsageNotes Always(string note) => new(note, note, note);

    public string? For(AiUsageBasis basis) => basis switch
    {
        AiUsageBasis.Subscription => Subscription,
        AiUsageBasis.ApiKey => ApiKey,
        _ => Unknown,
    };
}
