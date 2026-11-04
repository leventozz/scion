using Scion.Application.Projects;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Adapting;

/// <summary>
/// Saves a reviewed adaptation through the copy pipeline: the whole skill folder goes to the target with the adapted
/// SKILL.md. Only an explicit overwrite replaces a skill the target already has.
/// </summary>
public sealed class SaveAdaptedSkill(
    ISkillFolderNames skillFolderNames,
    IAdaptedSkillWriter adaptedSkillWriter,
    IFolderProbe folderProbe,
    PathComparison pathComparison)
{
    public async Task<Result<SkillCopyOutcome, SaveAdaptedSkillError>> ExecuteAsync(
        AdaptationRequest request,
        string adaptedText,
        ConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        if (!folderProbe.Exist(request.Target.Location))
        {
            return Failure(SaveAdaptedSkillError.TargetFolderMissing);
        }

        // Read now, not at review time: a skill that appeared since must still be treated as a conflict.
        var existing = await skillFolderNames.ListAsync(request.Target, cancellationToken);
        var exists = existing.Any(name => pathComparison.Same(name, request.SourceFolderName));

        if (exists && resolution != ConflictResolution.Overwrite)
        {
            return Failure(SaveAdaptedSkillError.AlreadyExists);
        }

        var skillFile = request.SourceFormat.Encode(adaptedText);
        try
        {
            if (exists)
            {
                await adaptedSkillWriter.ReplaceAsync(
                    request.Source, request.SourceFolderName, request.Target, skillFile, cancellationToken);
                return Result<SkillCopyOutcome, SaveAdaptedSkillError>.Success(SkillCopyOutcome.Overwritten);
            }

            await adaptedSkillWriter.CopyAsync(
                request.Source, request.SourceFolderName, request.Target, skillFile, cancellationToken);
            return Result<SkillCopyOutcome, SaveAdaptedSkillError>.Success(SkillCopyOutcome.Copied);
        }
        catch (SkillCopyFailedException)
        {
            return Failure(SaveAdaptedSkillError.Failed);
        }
    }

    private static Result<SkillCopyOutcome, SaveAdaptedSkillError> Failure(SaveAdaptedSkillError error) =>
        Result<SkillCopyOutcome, SaveAdaptedSkillError>.Failure(error);
}
