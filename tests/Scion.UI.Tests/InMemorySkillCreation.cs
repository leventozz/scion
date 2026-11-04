using Scion.Application.Creating;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>Creates a skill where the viewing and listing fakes can see it: the list and the files of its page.</summary>
internal sealed class InMemorySkillCreation(InMemorySkillFolderReader reader, InMemorySkillFiles skillFiles) : ISkillFolderCreator
{
    private SkillCreateFailure? _failure;
    private TaskCompletionSource? _gate;

    public List<string> Created { get; } = [];

    public void FailWith(SkillCreateFailure? failure) => _failure = failure;

    /// <summary>Creation waits until the returned source is completed, so a test can look at the dialog while it creates.</summary>
    public TaskCompletionSource HoldCreation() => _gate = new TaskCompletionSource();

    public async Task CreateAsync(AgentLocation place, string folderName, string skillFile, CancellationToken cancellationToken)
    {
        if (_gate is { } gate)
        {
            await gate.Task;
        }

        if (_failure is { } failure)
        {
            throw new SkillCreateFailedException(failure);
        }

        if ((await reader.ReadAsync(place.Location, place.Agent, cancellationToken)).Any(folder => folder.FolderName == folderName))
        {
            throw new SkillCreateFailedException(SkillCreateFailure.AlreadyExists);
        }

        var description = skillFile.Split('\n').First(line => line.StartsWith("description: "))["description: ".Length..];
        reader.Put(place.Location, place.Agent, new SkillFolder(folderName, folderName, description));
        skillFiles.Add(place.Location, place.Agent, folderName, $"/skills/{folderName}", ("SKILL.md", skillFile));
        Created.Add(folderName);
    }
}
