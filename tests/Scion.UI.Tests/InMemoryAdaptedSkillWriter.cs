using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;

namespace Scion.UI.Tests;

/// <summary>Puts the adapted skill into the in-memory skill list, so a save shows up the next time a screen lists skills.</summary>
internal sealed class InMemoryAdaptedSkillWriter(InMemorySkillFolderReader reader) : IAdaptedSkillWriter
{
    private bool _fails;

    public List<(string FolderName, string SkillFile, bool Replaced)> Written { get; } = [];

    /// <summary>Where each skill was written, in the order of <see cref="Written"/>.</summary>
    public List<AgentLocation> Targets { get; } = [];

    public void Fails() => _fails = true;

    public async Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken)
    {
        if ((await reader.ReadAsync(target.Location, target.Agent, cancellationToken)).Any(folder => folder.FolderName == folderName))
        {
            throw new SkillCopyFailedException(folderName);
        }

        Put(folderName, target, skillFile, replaced: false);
    }

    public Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken)
    {
        Put(folderName, target, skillFile, replaced: true);

        return Task.CompletedTask;
    }

    private void Put(string folderName, AgentLocation target, byte[] skillFile, bool replaced)
    {
        if (_fails)
        {
            throw new SkillCopyFailedException(folderName);
        }

        Targets.Add(target);
        reader.Put(target.Location, target.Agent, new SkillFolder(folderName, folderName, "Adapted"));
        Written.Add((folderName, Encoding.UTF8.GetString(skillFile), replaced));
    }
}
