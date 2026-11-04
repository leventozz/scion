using Scion.Application.Projects;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;
using Scion.Domain.Skills;

namespace Scion.Application.Creating;

public sealed class CreateSkill(
    IFolderProbe folderProbe,
    ISkillFolderNames skillFolderNames,
    ISkillFrontmatterWriter frontmatterWriter,
    ISkillFolderCreator skillFolderCreator,
    PathComparison pathComparison)
{
    public async Task<Result<SkillSummary, CreateSkillError>> ExecuteAsync(
        AgentLocation place, string name, string description, CancellationToken cancellationToken = default)
    {
        if (!folderProbe.Exist(place.Location))
        {
            return Failure(CreateSkillError.FolderMissing);
        }

        // Read now, not when the dialog opened: a folder that appeared since must still be treated as taken.
        var form = new NewSkillForm(await skillFolderNames.ListAsync(place, cancellationToken), pathComparison);
        var check = form.Check(name, description);
        if (!check.CanCreate)
        {
            return Failure(check.NameProblem == NewSkillProblem.NameTaken ? CreateSkillError.AlreadyExists : CreateSkillError.Invalid);
        }

        var skillName = SkillName.Create(name).Value;
        var skillDescription = SkillDescription.Create(NewSkillDescription.Normalize(description)).Value;
        var skillFile = SkillTemplate.Compose(frontmatterWriter.Write(skillName, skillDescription));

        try
        {
            await skillFolderCreator.CreateAsync(place, skillName.Value, skillFile, cancellationToken);
        }
        catch (SkillCreateFailedException exception)
        {
            return Failure(exception.Failure == SkillCreateFailure.AlreadyExists
                ? CreateSkillError.AlreadyExists
                : CreateSkillError.WriteFailed);
        }

        return Result<SkillSummary, CreateSkillError>.Success(
            new SkillSummary(skillName.Value, skillName.Value, skillDescription.Value, [], CanBeDeleted: true));
    }

    private static Result<SkillSummary, CreateSkillError> Failure(CreateSkillError error) =>
        Result<SkillSummary, CreateSkillError>.Failure(error);
}
