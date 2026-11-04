using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>
/// Both copying ports over the in-memory skill reader, so a copy shows up the next time a screen lists skills.
/// </summary>
internal sealed class InMemorySkillFolders(InMemorySkillFolderReader reader)
    : ISkillFolderNames, ISkillFolderCopier, ISkillFolderComparer
{
    private readonly HashSet<string> _failing = [];

    public List<string> Copied { get; } = [];

    public List<string> Replaced { get; } = [];

    public void FailOn(string folderName) => _failing.Add(folderName);

    public void StopFailing() => _failing.Clear();

    public async Task<IReadOnlyList<string>> ListAsync(AgentLocation place, CancellationToken cancellationToken) =>
        (await reader.ReadAsync(place.Location, place.Agent, cancellationToken)).Select(folder => folder.FolderName).ToList();

    public async Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        var skill = await SourceSkillAsync(source, folderName, cancellationToken);
        if ((await ListAsync(target, cancellationToken)).Contains(folderName))
        {
            throw new SkillCopyFailedException(folderName);
        }

        reader.Put(target.Location, target.Agent, skill);
        Copied.Add(folderName);
    }

    public async Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        var skill = await SourceSkillAsync(source, folderName, cancellationToken);

        reader.Put(target.Location, target.Agent, skill);
        Replaced.Add(folderName);
    }

    /// <summary>Two skills are identical when the reader holds equal data for them (same name and description).</summary>
    public async Task<bool> AreIdenticalAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        var sourceSkill = await SourceSkillAsync(source, folderName, cancellationToken);
        var targetSkill = (await reader.ReadAsync(target.Location, target.Agent, cancellationToken))
            .SingleOrDefault(folder => folder.FolderName == folderName);

        return sourceSkill == targetSkill;
    }

    private async Task<SkillFolder> SourceSkillAsync(
        AgentLocation source, string folderName, CancellationToken cancellationToken)
    {
        var skill = (await reader.ReadAsync(source.Location, source.Agent, cancellationToken))
            .SingleOrDefault(folder => folder.FolderName == folderName);

        return skill is null || _failing.Contains(folderName) ? throw new SkillCopyFailedException(folderName) : skill;
    }
}
