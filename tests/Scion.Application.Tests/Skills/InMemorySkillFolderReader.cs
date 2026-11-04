using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.Application.Tests.Skills;

internal sealed class InMemorySkillFolderReader : ISkillFolderReader
{
    private readonly Dictionary<(Location, Agent), IReadOnlyList<SkillFolder>> _skillFolders = new();

    public void Add(Location location, Agent agent, params SkillFolder[] skillFolders) =>
        _skillFolders[(location, agent)] = skillFolders;

    public Task<IReadOnlyList<SkillFolder>> ReadAsync(
        Location location, Agent agent, CancellationToken cancellationToken)
    {
        var found = _skillFolders.TryGetValue((location, agent), out var skillFolders)
            ? skillFolders
            : [];

        return Task.FromResult(found);
    }
}
