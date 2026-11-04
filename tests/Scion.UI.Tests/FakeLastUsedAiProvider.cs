using Scion.Application.Adapting;

namespace Scion.UI.Tests;

internal sealed class FakeLastUsedAiProvider : ILastUsedAiProvider
{
    public AiProviderId? Remembered { get; set; }

    public int Writes { get; private set; }

    public Task<AiProviderId?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Remembered);

    public Task RememberAsync(AiProviderId provider, CancellationToken cancellationToken)
    {
        Writes++;
        Remembered = provider;

        return Task.CompletedTask;
    }
}
