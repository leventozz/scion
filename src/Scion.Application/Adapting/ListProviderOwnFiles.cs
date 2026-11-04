namespace Scion.Application.Adapting;

public sealed class ListProviderOwnFiles(IAiProviderPorts<IAiProviderOwnFiles> ownFiles)
{
    public Task<IReadOnlyList<ProviderLoadedFile>> ExecuteAsync(AiProviderId provider, CancellationToken cancellationToken = default) =>
        ownFiles.For(provider).FindAsync(cancellationToken);
}
