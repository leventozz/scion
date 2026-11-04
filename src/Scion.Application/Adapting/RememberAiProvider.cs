namespace Scion.Application.Adapting;

/// <summary>Notes the provider the user adapts with. A note that cannot be written never stops the adaptation.</summary>
public sealed class RememberAiProvider(ILastUsedAiProvider lastUsed)
{
    public async Task ExecuteAsync(AiProviderId provider, CancellationToken cancellationToken = default)
    {
        try
        {
            await lastUsed.RememberAsync(provider, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
