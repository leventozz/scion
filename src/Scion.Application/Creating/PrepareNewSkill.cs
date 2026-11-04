using Scion.Application.Skills.Copying;

namespace Scion.Application.Creating;

public sealed class PrepareNewSkill(ISkillFolderNames skillFolderNames, PathComparison pathComparison)
{
    public async Task<NewSkillForm> ExecuteAsync(AgentLocation place, CancellationToken cancellationToken = default) =>
        new(await skillFolderNames.ListAsync(place, cancellationToken), pathComparison);
}
