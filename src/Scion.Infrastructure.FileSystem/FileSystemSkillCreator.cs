using System.Text;
using Scion.Application.Creating;
using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Creates a new skill folder on disk. The folder is built in the scratch directory of the agent folder and moved
/// into place, so a failure never leaves a half-made skill, and a folder that is already there is never touched.
/// </summary>
public sealed class FileSystemSkillCreator : ISkillFolderCreator
{
    private const string SkillFileName = "SKILL.md";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly SkillsDirectories _skillsDirectories;
    private readonly Action<string, string> _moveDirectory;

    public FileSystemSkillCreator(string homeDirectory, IEnumerable<IAgentLayout> layouts)
        : this(homeDirectory, layouts, Directory.Move)
    {
    }

    /// <param name="moveDirectory">The one step that tests replace to make a move fail.</param>
    internal FileSystemSkillCreator(
        string homeDirectory, IEnumerable<IAgentLayout> layouts, Action<string, string> moveDirectory)
    {
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);
        _moveDirectory = moveDirectory;
    }

    public async Task CreateAsync(
        AgentLocation place, string folderName, string skillFile, CancellationToken cancellationToken)
    {
        SkillFolderName.EnsureIsSingleSegment(
            folderName, _ => new SkillCreateFailedException(SkillCreateFailure.Other));

        try
        {
            await Task.Run(() => Create(place, folderName, skillFile, cancellationToken), cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SkillCreateFailedException(SkillCreateFailure.AccessDenied, exception);
        }
        catch (IOException exception)
        {
            throw new SkillCreateFailedException(SkillCreateFailure.Other, exception);
        }
    }

    private void Create(AgentLocation place, string folderName, string skillFile, CancellationToken cancellationToken)
    {
        var skillsDirectory = _skillsDirectories.For(place.Location, place.Agent);
        var scratch = CopyScratch.BesideSkills(skillsDirectory);
        var target = Path.Combine(skillsDirectory, folderName);
        var staging = CopyScratch.NewStaging(scratch, folderName);

        try
        {
            Directory.CreateDirectory(skillsDirectory);
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, SkillFileName), skillFile, Utf8WithoutBom);
            cancellationToken.ThrowIfCancellationRequested();

            MoveIn(staging, target);
        }
        finally
        {
            BestEffort.Run(() => DeleteDirectory(staging));
            BestEffort.DeleteDirectoryIfEmpty(scratch);
        }
    }

    // Directory.Move silently replaces an empty directory on some platforms, so look first. The move can still lose
    // a race against another writer; whatever made it fail, a folder that is there now was not made by us.
    private void MoveIn(string staging, string target)
    {
        if (Directory.Exists(target))
        {
            throw new SkillCreateFailedException(SkillCreateFailure.AlreadyExists);
        }

        try
        {
            _moveDirectory(staging, target);
        }
        catch (IOException) when (Directory.Exists(target))
        {
            throw new SkillCreateFailedException(SkillCreateFailure.AlreadyExists);
        }
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
