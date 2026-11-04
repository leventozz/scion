namespace Scion.Application.Creating;

public sealed record NewSkillCheck(NewSkillProblem? NameProblem, NewSkillProblem? DescriptionProblem)
{
    public bool CanCreate => NameProblem is null && DescriptionProblem is null;
}
