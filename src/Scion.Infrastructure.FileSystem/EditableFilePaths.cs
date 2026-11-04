using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Turns the address of an editable file into a path on disk. A path that is not inside the skill folder, or an
/// instruction file that is not in the agent's convention, is a bug in the caller: it throws instead of touching
/// the file.
/// </summary>
internal sealed class EditableFilePaths
{
    private readonly string _homeDirectory;
    private readonly SkillsDirectories _skillsDirectories;
    private readonly IReadOnlyList<IInstructionFileLayout> _instructionLayouts;

    public EditableFilePaths(
        string homeDirectory, IEnumerable<IAgentLayout> layouts, IEnumerable<IInstructionFileLayout> instructionLayouts)
    {
        _homeDirectory = homeDirectory;
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);
        _instructionLayouts = [.. instructionLayouts];
    }

    public string PathOf(EditableFile file) => file switch
    {
        EditableFile.SkillFile skillFile => SkillFilePath(skillFile),
        EditableFile.InstructionFile instructionFile => InstructionFilePath(instructionFile),
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, null),
    };

    /// <summary>The scratch directory of the agent folder that holds the skill (beside the skills directory).</summary>
    public string ScratchOf(EditableFile.SkillFile file) =>
        CopyScratch.BesideSkills(_skillsDirectories.For(file.Location, file.Agent));

    private string SkillFilePath(EditableFile.SkillFile file)
    {
        SkillFolderName.EnsureIsSingleSegment(file.FolderName, name => new ArgumentException($"'{name}' is not a skill folder name."));

        var folder = Path.Combine(_skillsDirectories.For(file.Location, file.Agent), file.FolderName);
        var path = Path.GetFullPath(Path.Combine(folder, file.RelativePath));
        EnsureInside(folder, path);

        return path;
    }

    private string InstructionFilePath(EditableFile.InstructionFile file)
    {
        var layout = _instructionLayouts.Single(candidate => candidate.Agent == file.Agent);
        var convention = file.Location is Location.Personal ? layout.PersonalInstructionFiles : layout.ProjectInstructionFiles;

        if (!convention.Contains(file.RelativePath, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{file.RelativePath}' is not an instruction file of {file.Agent}.");
        }

        return Path.Combine(LocationRoot.Of(file.Location, _homeDirectory), file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void EnsureInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), path);
        var leavesFolder = relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative);

        if (leavesFolder)
        {
            throw new ArgumentException($"'{path}' is not inside the skill folder.");
        }
    }
}
