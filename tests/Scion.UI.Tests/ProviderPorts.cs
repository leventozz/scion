using Scion.Application.Adapting;

namespace Scion.UI.Tests;

/// <summary>The ports of the providers set up for a test. Asked for any other provider it fails, so a use case that picks the wrong one turns a test red.</summary>
internal sealed class ProviderPorts<TPort> : IAiProviderPorts<TPort>
{
    private readonly Dictionary<AiProviderId, TPort> _ports = [];

    public ProviderPorts<TPort> With(AiProviderId provider, TPort port)
    {
        _ports[provider] = port;

        return this;
    }

    public TPort For(AiProviderId requested) =>
        _ports.TryGetValue(requested, out var port)
            ? port
            : throw new InvalidOperationException(
                $"The port of provider '{requested.Value}' was asked for, but only {string.Join(", ", _ports.Keys.Select(key => $"'{key.Value}'"))} are set up.");
}
