using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Deleting;

public sealed class DeleteSkill(ISkillTrash trash, TimeProvider time)
{
    public async Task<Result<SkillDeletion, DeleteSkillError>> ExecuteAsync(
        AgentLocation place, string folderName, CancellationToken cancellationToken = default)
    {
        try
        {
            var trashed = await trash.MoveToTrashAsync(place, folderName, time.GetUtcNow(), cancellationToken);

            return Result<SkillDeletion, DeleteSkillError>.Success(
                trashed.OriginalFullyRemoved ? SkillDeletion.MovedToTrash : SkillDeletion.OriginalNotFullyRemoved);
        }
        catch (SkillTrashFailedException exception)
        {
            return Result<SkillDeletion, DeleteSkillError>.Failure(exception.Failure switch
            {
                SkillTrashFailure.Missing => DeleteSkillError.SkillMissing,
                SkillTrashFailure.Linked => DeleteSkillError.Linked,
                _ => DeleteSkillError.Failed,
            });
        }
    }
}
