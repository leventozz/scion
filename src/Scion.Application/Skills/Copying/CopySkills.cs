using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Application.Skills.Copying;

public sealed class CopySkills(
    IFolderProbe folderProbe,
    ISkillFolderNames skillFolderNames,
    ISkillFolderCopier skillFolderCopier,
    ISkillFolderComparer skillFolderComparer,
    PathComparison pathComparison)
{
    public async Task<Result<CopyReport, CopySkillsError>> ExecuteAsync(
        CopySkillsRequest request, CancellationToken cancellationToken = default)
    {
        if (CheckPlaces(request) is { } error)
        {
            return Result<CopyReport, CopySkillsError>.Failure(error);
        }

        // Read now, not at preview time: a conflict that appeared since must still be treated as one.
        var existingNames = await skillFolderNames.ListAsync(request.Target, cancellationToken);
        var skills = new List<CopiedSkill>();

        foreach (var folderName in WithoutDuplicates(request.FolderNames))
        {
            var exists = existingNames.Any(name => pathComparison.Same(name, folderName));
            skills.Add(new CopiedSkill(folderName, await CopyOneAsync(request, folderName, exists, cancellationToken)));
        }

        return Result<CopyReport, CopySkillsError>.Success(new CopyReport(skills));
    }

    private CopySkillsError? CheckPlaces(CopySkillsRequest request)
    {
        if (request.Source.Agent == request.Target.Agent
            && pathComparison.Same(request.Source.Location, request.Target.Location))
        {
            return CopySkillsError.SameSourceAndTarget;
        }

        if (!folderProbe.Exist(request.Source.Location))
        {
            return CopySkillsError.SourceFolderMissing;
        }

        return folderProbe.Exist(request.Target.Location) ? null : CopySkillsError.TargetFolderMissing;
    }

    private IEnumerable<string> WithoutDuplicates(IEnumerable<string> folderNames)
    {
        var seen = new List<string>();
        foreach (var folderName in folderNames)
        {
            if (!seen.Any(name => pathComparison.Same(name, folderName)))
            {
                seen.Add(folderName);
                yield return folderName;
            }
        }
    }

    private async Task<SkillCopyOutcome> CopyOneAsync(
        CopySkillsRequest request, string folderName, bool existsInTarget, CancellationToken cancellationToken)
    {
        try
        {
            if (!existsInTarget)
            {
                await skillFolderCopier.CopyAsync(request.Source, folderName, request.Target, cancellationToken);
                return SkillCopyOutcome.Copied;
            }

            if (await skillFolderComparer.AreIdenticalOrDifferentAsync(
                    request.Source, folderName, request.Target, cancellationToken))
            {
                return SkillCopyOutcome.Identical;
            }

            if (!request.Resolutions.TryGetValue(folderName, out var resolution)
                || resolution != ConflictResolution.Overwrite)
            {
                return SkillCopyOutcome.Skipped;
            }

            await skillFolderCopier.ReplaceAsync(request.Source, folderName, request.Target, cancellationToken);
            return SkillCopyOutcome.Overwritten;
        }
        catch (SkillCopyFailedException)
        {
            return SkillCopyOutcome.Failed;
        }
    }
}
