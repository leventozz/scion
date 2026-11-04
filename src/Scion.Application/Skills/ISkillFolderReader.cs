using Scion.Domain;

namespace Scion.Application.Skills;

public interface ISkillFolderReader
{
    Task<IReadOnlyList<SkillFolder>> ReadAsync(
        Location location, Agent agent, CancellationToken cancellationToken);
}
