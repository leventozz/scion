using Scion.Application.Skills.Copying;

namespace Scion.Application.Creating;

public interface ISkillFolderCreator
{
    /// <summary>
    /// Creates the skill folder with <paramref name="skillFile"/> as its SKILL.md. Never replaces a folder: if one of
    /// that name is there, nothing is touched and <see cref="SkillCreateFailedException"/> carries
    /// <see cref="SkillCreateFailure.AlreadyExists"/>.
    /// </summary>
    Task CreateAsync(AgentLocation place, string folderName, string skillFile, CancellationToken cancellationToken);
}
