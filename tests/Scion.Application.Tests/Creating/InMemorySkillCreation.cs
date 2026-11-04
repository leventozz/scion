using Scion.Application.Creating;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Skills.Copying;

namespace Scion.Application.Tests.Creating;

/// <summary>Creates skills in an <see cref="InMemorySkillFolders"/> and keeps the SKILL.md it was given.</summary>
internal sealed class InMemorySkillCreation(InMemorySkillFolders folders) : ISkillFolderCreator
{
    private readonly Dictionary<(AgentLocation, string), string> _skillFiles = new();
    private SkillCreateFailure? _failure;

    public void FailWith(SkillCreateFailure failure) => _failure = failure;

    public string SkillFileOf(AgentLocation place, string folderName) => _skillFiles[(place, folderName)];

    public Task CreateAsync(AgentLocation place, string folderName, string skillFile, CancellationToken cancellationToken)
    {
        if (_failure is { } failure)
        {
            throw new SkillCreateFailedException(failure);
        }

        if (folders.Has(place, folderName))
        {
            throw new SkillCreateFailedException(SkillCreateFailure.AlreadyExists);
        }

        folders.Add(place, folderName);
        _skillFiles[(place, folderName)] = skillFile;
        return Task.CompletedTask;
    }
}
