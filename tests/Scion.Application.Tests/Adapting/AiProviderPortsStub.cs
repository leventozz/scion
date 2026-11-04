using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>The ports of several providers, found by id. An id that was not set up is a test error, not a silent fallback.</summary>
internal sealed class AiProviderPortsStub<TPort> : IAiProviderPorts<TPort>
{
    private readonly Dictionary<AiProviderId, TPort> _ports = [];

    public AiProviderPortsStub<TPort> With(string providerId, TPort port)
    {
        _ports[new AiProviderId(providerId)] = port;

        return this;
    }

    public TPort For(AiProviderId provider) =>
        _ports.TryGetValue(provider, out var port)
            ? port
            : throw new InvalidOperationException($"No port was set up for provider '{provider.Value}'.");
}
