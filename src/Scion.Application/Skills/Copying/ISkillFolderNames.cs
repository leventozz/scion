namespace Scion.Application.Skills.Copying;

public interface ISkillFolderNames
{
    /// <summary>
    /// Every sub-folder name in the skills folder of <paramref name="place"/>, with or without a SKILL.md,
    /// because any of them would be overwritten by a copy. A missing skills folder gives an empty list.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(AgentLocation place, CancellationToken cancellationToken);
}
