namespace Scion.Application.Adapting;

public sealed class CheckAiProvider(IAiProviderPorts<IAiProviderStatus> statuses)
{
    public Task<AiProviderState> ExecuteAsync(AiProviderId provider, CancellationToken cancellationToken = default) =>
        statuses.For(provider).GetStateAsync(cancellationToken);
}
