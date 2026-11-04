using Microsoft.Extensions.DependencyInjection;
using Scion.Application.Adapting;

namespace Scion.Host.Desktop;

/// <summary>
/// Finds a provider's port among the services registered with that provider's id as key. Only the composition root knows
/// the container: the use cases ask <see cref="IAiProviderPorts{TPort}"/> and never see it.
/// </summary>
internal sealed class KeyedAiProviderPorts<TPort>(IServiceProvider services) : IAiProviderPorts<TPort>
    where TPort : notnull
{
    public TPort For(AiProviderId provider) => services.GetRequiredKeyedService<TPort>(provider);
}
