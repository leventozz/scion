using Scion.Application.Editing;
using Scion.Application.Projects;
using Scion.Application.Skills.Copying;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Adapting;

public sealed class PrepareAdaptation(
    OpenFileForEditing openFileForEditing,
    ITargetContextReader targetContextReader,
    ISkillFrontmatterParser frontmatterParser,
    IFolderProbe folderProbe,
    PathComparison pathComparison)
{
    public async Task<Result<AdaptationRequest, PrepareAdaptationError>> ExecuteAsync(
        AgentLocation source,
        string folderName,
        AgentLocation target,
        string targetName,
        CancellationToken cancellationToken = default)
    {
        if (source.Agent == target.Agent && pathComparison.Same(source.Location, target.Location))
        {
            return Failure(PrepareAdaptationError.SameSourceAndTarget);
        }

        if (!folderProbe.Exist(target.Location))
        {
            return Failure(PrepareAdaptationError.TargetFolderMissing);
        }

        var skillFile = new EditableFile.SkillFile(source.Location, source.Agent, folderName, "SKILL.md");
        var opened = await openFileForEditing.ExecuteAsync(skillFile, cancellationToken);
        if (!opened.IsSuccess)
        {
            return Failure(PrepareAdaptationError.SourceUnreadable);
        }

        var context = await ReadContextAsync(target, targetName, cancellationToken);
        var skillName = frontmatterParser.Parse(opened.Value.Text).Name is { Length: > 0 } name ? name : folderName;

        return Result<AdaptationRequest, PrepareAdaptationError>.Success(new AdaptationRequest(
            source,
            folderName,
            skillName,
            opened.Value.Text,
            opened.Value.Baseline.Format,
            target,
            targetName,
            context.Files,
            context.FolderNames,
            ExtraInstructions: "",
            context.Omitted));
    }

    private async Task<ContextLimits.Fitted> ReadContextAsync(
        AgentLocation target, string targetName, CancellationToken cancellationToken)
    {
        var limits = ContextLimits.Default;
        var found = await targetContextReader.ReadAsync(target, limits, cancellationToken);

        // Personal is the user's home: only its instruction file is ever shared, whatever the reader found there.
        var isPersonal = target.Location is Location.Personal;
        var files = found.Files
            .Where(file => !isPersonal || file.Kind == TargetContextFileKind.InstructionFile)
            .OrderBy(file => file.Kind)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => Shared(file, targetName))
            .OfType<SharedFile>();

        return limits.Fit(files, isPersonal ? [] : found.FolderNames, SharedItemLabels.FolderNames(targetName));
    }

    private static SharedFile? Shared(TargetContextFile file, string targetName)
    {
        if (FileText.Decode(new FileBytes(file.Bytes, file.Bytes.Length)) is not FileText.Shown shown)
        {
            return null;
        }

        var label = file.Kind == TargetContextFileKind.InstructionFile ? $"{file.RelativePath} from {targetName}" : file.RelativePath;

        return new SharedFile(label, LineBreaks.Normalize(shown.Text), file.Bytes.Length);
    }

    private static Result<AdaptationRequest, PrepareAdaptationError> Failure(PrepareAdaptationError error) =>
        Result<AdaptationRequest, PrepareAdaptationError>.Failure(error);
}
