using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Skills.Copying;

public sealed class PreviewSkillCopy(
    IFolderProbe folderProbe,
    ISkillFolderNames skillFolderNames,
    ISkillFolderComparer skillFolderComparer,
    PathComparison pathComparison)
{
    public async Task<Result<CopyPreview, CopySkillsError>> ExecuteAsync(
        AgentLocation source,
        AgentLocation target,
        IReadOnlyList<string> folderNames,
        CancellationToken cancellationToken = default)
    {
        if (!folderProbe.Exist(target.Location))
        {
            return Result<CopyPreview, CopySkillsError>.Failure(CopySkillsError.TargetFolderMissing);
        }

        var existing = await skillFolderNames.ListAsync(target, cancellationToken);
        var different = new List<string>();
        var identical = new List<string>();

        foreach (var folderName in folderNames.Where(name => existing.Any(other => pathComparison.Same(other, name))))
        {
            var isIdentical = await skillFolderComparer.AreIdenticalOrDifferentAsync(
                source, folderName, target, cancellationToken);
            (isIdentical ? identical : different).Add(folderName);
        }

        return Result<CopyPreview, CopySkillsError>.Success(new CopyPreview(different, identical));
    }
}
