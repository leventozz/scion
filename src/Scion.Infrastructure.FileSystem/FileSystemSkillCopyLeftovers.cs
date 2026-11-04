using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemSkillCopyLeftovers : ISkillCopyLeftovers
{
    private readonly SkillsDirectories _skillsDirectories;

    public FileSystemSkillCopyLeftovers(string homeDirectory, IEnumerable<IAgentLayout> layouts) =>
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);

    public Task CleanUpAsync(AgentLocation place, CancellationToken cancellationToken) =>
        Task.Run(() => CleanUp(_skillsDirectories.For(place.Location, place.Agent)), cancellationToken);

    private static void CleanUp(string skillsDirectory)
    {
        var scratch = CopyScratch.BesideSkills(skillsDirectory);

        try
        {
            if (!Directory.Exists(scratch))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(scratch).ToList())
            {
                ClearOne(directory, skillsDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(scratch).ToList())
            {
                if (CopyScratch.IsEditStaging(Path.GetFileName(file)))
                {
                    File.Delete(file);
                }
            }

            if (!Directory.EnumerateFileSystemEntries(scratch).Any())
            {
                Directory.Delete(scratch);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: whatever is left is tried again at the next start.
        }
    }

    private static void ClearOne(string directory, string skillsDirectory)
    {
        var name = Path.GetFileName(directory);

        if (CopyScratch.TryGetPreviousSkill(name, out var skillName))
        {
            var skill = Path.Combine(skillsDirectory, skillName);
            if (!Directory.Exists(skill))
            {
                // The swap broke after the old folder was parked: this is the only copy of the skill.
                Directory.CreateDirectory(skillsDirectory);
                Directory.Move(directory, skill);
                return;
            }

            Directory.Delete(directory, recursive: true);
        }
        else if (CopyScratch.IsStaging(name))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
