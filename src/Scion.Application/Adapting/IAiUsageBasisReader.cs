namespace Scion.Application.Adapting;

public interface IAiUsageBasisReader
{
    /// <summary>
    /// Asks the provider's own client how it is signed in. Only the kind of sign-in is taken from the answer: never a
    /// credential, an account or an organization. Anything that cannot be told is <see cref="AiUsageBasis.Unknown"/>.
    /// </summary>
    Task<AiUsageBasis> ReadAsync(CancellationToken cancellationToken);
}
