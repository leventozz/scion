using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>Providers with neutral sample names for tests.</summary>
internal static class AiProviders
{
    public static AiProvider Sample(string id = "sample") =>
        new(
            new AiProviderId(id),
            $"Provider {id}",
            $"Client {id}",
            $"Account {id}",
            new Uri($"https://example.com/{id}/install"),
            new AiUsageNotes("Counts toward the plan.", "Uses the API key."));
}
