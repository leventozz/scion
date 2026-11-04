using Scion.Application.Adapting;

namespace Scion.UI.Tests;

/// <summary>The files a provider's client loads by itself, as a test decided.</summary>
internal sealed class FakeOwnFiles : IAiProviderOwnFiles
{
    public List<ProviderLoadedFile> Files { get; } = [];

    public Task<IReadOnlyList<ProviderLoadedFile>> FindAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderLoadedFile>>([.. Files]);
}
