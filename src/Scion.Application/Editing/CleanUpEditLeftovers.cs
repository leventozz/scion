using Scion.Application.Projects;

namespace Scion.Application.Editing;

/// <summary>Meant to run when the app starts: a save that was cut short may have left a temp file beside an instruction file.</summary>
public sealed class CleanUpEditLeftovers(StartupLocations startupLocations, IInstructionFileLeftovers instructionFileLeftovers)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        foreach (var location in await startupLocations.ListAsync(cancellationToken))
        {
            await instructionFileLeftovers.CleanUpAsync(location, cancellationToken);
        }
    }
}
