namespace Scion.Application.Adapting;

/// <summary>
/// Finds the port of one provider by its id. It is generic so that a use case depends only on the one port it needs
/// (a status check never sees the adapter), and the composition root decides how the ports are registered.
/// </summary>
public interface IAiProviderPorts<out TPort>
{
    TPort For(AiProviderId provider);
}
