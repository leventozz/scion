namespace Scion.Application.Adapting;

public sealed class CheckAiUsageBasis(IAiProviderPorts<IAiUsageBasisReader> readers)
{
    public Task<AiUsageBasis> ExecuteAsync(AiProviderId provider, CancellationToken cancellationToken = default) =>
        readers.For(provider).ReadAsync(cancellationToken);
}
