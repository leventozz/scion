using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Skills.Copying;

namespace Scion.Application.Tests.Adapting;

/// <summary>Writes into the same folders the names port reads, the way the real adapter does.</summary>
internal sealed class InMemoryAdaptedSkillWriter(InMemorySkillFolders folders) : IAdaptedSkillWriter
{
    private bool _fails;

    public List<WrittenSkill> Written { get; } = [];

    public void Fails() => _fails = true;

    public Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken)
    {
        if (_fails || folders.Has(target, folderName))
        {
            throw new SkillCopyFailedException(folderName);
        }

        folders.Add(target, folderName);
        Written.Add(new WrittenSkill(source, folderName, target, skillFile, Replaced: false));

        return Task.CompletedTask;
    }

    public Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, byte[] skillFile, CancellationToken cancellationToken)
    {
        if (_fails)
        {
            throw new SkillCopyFailedException(folderName);
        }

        folders.Add(target, folderName);
        Written.Add(new WrittenSkill(source, folderName, target, skillFile, Replaced: true));

        return Task.CompletedTask;
    }

    internal sealed record WrittenSkill(
        AgentLocation Source, string FolderName, AgentLocation Target, byte[] SkillFile, bool Replaced);
}
