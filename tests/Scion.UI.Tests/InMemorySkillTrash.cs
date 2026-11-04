using Scion.Application.Deleting;
using Scion.Application.Skills.Copying;

namespace Scion.UI.Tests;

/// <summary>Takes a skill out of the in-memory skill list, as moving its folder to the trash takes it off the disk.</summary>
internal sealed class InMemorySkillTrash(InMemorySkillFolderReader reader) : ISkillTrash
{
    private SkillTrashFailure? _failure;
    private TaskCompletionSource? _gate;
    private bool _originalFullyRemoved = true;

    public List<string> Trashed { get; } = [];

    public void FailWith(SkillTrashFailure? failure) => _failure = failure;

    public void LeaveOriginalBehind() => _originalFullyRemoved = false;

    /// <summary>The move waits until the returned source is completed, so a test can look at the dialog while it runs.</summary>
    public TaskCompletionSource HoldMove() => _gate = new TaskCompletionSource();

    public async Task<TrashedSkill> MoveToTrashAsync(
        AgentLocation place, string folderName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_gate is { } gate)
        {
            await gate.Task;
        }

        if (_failure is { } failure)
        {
            throw new SkillTrashFailedException(failure);
        }

        if (!reader.Remove(place.Location, place.Agent, folderName))
        {
            throw new SkillTrashFailedException(SkillTrashFailure.Missing);
        }

        Trashed.Add(folderName);
        return new TrashedSkill(_originalFullyRemoved);
    }
}
