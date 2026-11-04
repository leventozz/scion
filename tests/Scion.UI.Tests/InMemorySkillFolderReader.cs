using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

internal sealed class InMemorySkillFolderReader : ISkillFolderReader
{
    private readonly Dictionary<(Location, Agent), IReadOnlyList<SkillFolder>> _skillFolders = new();

    public void AddPersonal(Agent agent, params SkillFolder[] skillFolders) =>
        _skillFolders[(new Location.Personal(), agent)] = skillFolders;

    public void AddProject(string path, Agent agent, params SkillFolder[] skillFolders) =>
        _skillFolders[(new Location.Project(path), agent)] = skillFolders;

    /// <summary>Adds the skill, or replaces the one with the same folder name.</summary>
    public void Put(Location location, Agent agent, SkillFolder skillFolder)
    {
        var others = _skillFolders.TryGetValue((location, agent), out var existing)
            ? existing.Where(folder => folder.FolderName != skillFolder.FolderName)
            : [];

        _skillFolders[(location, agent)] = [.. others, skillFolder];
    }

    /// <returns>Whether a skill of that folder name was there.</returns>
    public bool Remove(Location location, Agent agent, string folderName)
    {
        if (!_skillFolders.TryGetValue((location, agent), out var existing) || existing.All(folder => folder.FolderName != folderName))
        {
            return false;
        }

        _skillFolders[(location, agent)] = existing.Where(folder => folder.FolderName != folderName).ToList();
        return true;
    }

    public Task<IReadOnlyList<SkillFolder>> ReadAsync(
        Location location, Agent agent, CancellationToken cancellationToken)
    {
        var found = _skillFolders.TryGetValue((location, agent), out var skillFolders) ? skillFolders : [];

        return Task.FromResult(found);
    }
}
