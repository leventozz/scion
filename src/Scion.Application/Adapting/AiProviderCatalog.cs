namespace Scion.Application.Adapting;

/// <summary>The providers Scion can adapt with, in the order the user is offered them.</summary>
public sealed class AiProviderCatalog
{
    public AiProviderCatalog(IReadOnlyList<AiProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (providers.Count == 0)
        {
            throw new ArgumentException("A catalog needs at least one provider.", nameof(providers));
        }

        if (providers.Select(provider => provider.Id).Distinct().Count() != providers.Count)
        {
            throw new ArgumentException("Two providers have the same id.", nameof(providers));
        }

        Providers = providers;
    }

    public IReadOnlyList<AiProvider> Providers { get; }

    /// <summary>The provider the Adapt dialog uses: the first one.</summary>
    public AiProvider Default => Providers[0];
}
