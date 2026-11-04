using Scion.Domain;

namespace Scion.Application.Skills;

public sealed class ListSkills(ISkillFolderReader reader)
{
    public async Task<IReadOnlyList<SkillSummary>> ExecuteAsync(
        Location location, Agent agent, CancellationToken cancellationToken = default)
    {
        var skillFolders = await reader.ReadAsync(location, agent, cancellationToken);

        return skillFolders
            .Select(SkillValidation.Summarize)
            .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
