namespace Scion.Application.Adapting;

/// <summary>
/// Decides which provider the Adapt dialog starts with: the one used last time (even when it is not ready now, so the
/// dialog shows what to do about it), else the first ready one, else the first of the catalog.
/// </summary>
public sealed class ChooseAiProvider(AiProviderCatalog catalog, ILastUsedAiProvider lastUsed)
{
    public async Task<AiProvider> ExecuteAsync(
        IReadOnlyDictionary<AiProviderId, AiProviderState> states, CancellationToken cancellationToken = default)
    {
        var last = await lastUsed.ReadAsync(cancellationToken);
        var rememberedProvider = catalog.Providers.FirstOrDefault(provider => provider.Id == last);
        if (rememberedProvider is not null)
        {
            return rememberedProvider;
        }

        return catalog.Providers.FirstOrDefault(provider => states.GetValueOrDefault(provider.Id) == AiProviderState.Ready)
            ?? catalog.Default;
    }
}
