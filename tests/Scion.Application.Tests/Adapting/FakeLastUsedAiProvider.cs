using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

internal sealed class FakeLastUsedAiProvider : ILastUsedAiProvider
{
    public AiProviderId? Remembered { get; set; }

    public Exception? WriteFails { get; set; }

    public int Writes { get; private set; }

    public Task<AiProviderId?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Remembered);

    public Task RememberAsync(AiProviderId provider, CancellationToken cancellationToken)
    {
        Writes++;
        if (WriteFails is not null)
        {
            throw WriteFails;
        }

        Remembered = provider;

        return Task.CompletedTask;
    }
}
