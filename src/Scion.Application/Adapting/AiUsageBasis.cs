namespace Scion.Application.Adapting;

/// <summary>What using an AI provider costs the user, as the provider's own client reports it. Scion never finds this out itself.</summary>
public enum AiUsageBasis
{
    /// <summary>The client did not say, or says something Scion does not know: nothing is claimed.</summary>
    Unknown,

    /// <summary>The client works with the user's subscription.</summary>
    Subscription,

    /// <summary>The client works with an API key.</summary>
    ApiKey,
}
