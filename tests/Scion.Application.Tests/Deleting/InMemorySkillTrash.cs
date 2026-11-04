using Scion.Application.Deleting;
using Scion.Application.Skills.Copying;

namespace Scion.Application.Tests.Deleting;

/// <summary>Remembers what was moved to the trash, and when.</summary>
internal sealed class InMemorySkillTrash : ISkillTrash
{
    private SkillTrashFailure? _failure;
    private bool _originalFullyRemoved = true;

    public List<(AgentLocation Place, string FolderName, DateTimeOffset Now)> Trashed { get; } = [];

    public void FailWith(SkillTrashFailure failure) => _failure = failure;

    public void LeaveOriginalBehind() => _originalFullyRemoved = false;

    public Task<TrashedSkill> MoveToTrashAsync(
        AgentLocation place, string folderName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_failure is { } failure)
        {
            throw new SkillTrashFailedException(failure);
        }

        Trashed.Add((place, folderName, now));
        return Task.FromResult(new TrashedSkill(_originalFullyRemoved));
    }
}
