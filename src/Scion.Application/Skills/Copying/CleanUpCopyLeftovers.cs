using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Skills.Copying;

/// <summary>Meant to run when the app starts: every place Scion has copied into may hold leftovers.</summary>
public sealed class CleanUpCopyLeftovers(StartupLocations startupLocations, ISkillCopyLeftovers skillCopyLeftovers)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        foreach (var location in await startupLocations.ListAsync(cancellationToken))
        {
            foreach (var agent in Enum.GetValues<Agent>())
            {
                await skillCopyLeftovers.CleanUpAsync(new AgentLocation(location, agent), cancellationToken);
            }
        }
    }
}
