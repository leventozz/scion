using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Puts a skill folder into a target. New content is always built in the scratch directory of the agent folder first
/// and moved into place afterwards, so a failure never leaves a half-written skill and never loses the previous one.
/// What goes into the new folder is the caller's choice (<c>fill</c>): the plain copy and the adapted copy differ only there.
/// </summary>
internal sealed class SkillFolderTransfer(SkillsDirectories skillsDirectories, Action<string, string> moveDirectory)
{
    /// <summary>Fills an empty staging folder from the source skill folder.</summary>
    public delegate void Fill(string sourceDirectory, string stagingDirectory, CancellationToken cancellationToken);

    public static void CopyTree(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyTree(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)), cancellationToken);
        }
    }

    public static async Task RunAsync(string folderName, Action work)
    {
        try
        {
            await Task.Run(work);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SkillCopyFailedException(folderName, exception);
        }
    }

    public void Copy(
        AgentLocation source, string folderName, AgentLocation target, Fill fill, CancellationToken cancellationToken)
    {
        var paths = Prepare(source, folderName, target);

        try
        {
            var staging = Stage(paths, folderName, fill, cancellationToken);

            // Directory.Move silently replaces an empty directory on some platforms, so check ourselves.
            if (Directory.Exists(paths.TargetDirectory))
            {
                TryDelete(staging);
                throw new SkillCopyFailedException(folderName);
            }

            MoveIn(staging, paths.TargetDirectory);
        }
        finally
        {
            TryDeleteIfEmpty(paths.Scratch);
        }
    }

    public void Replace(
        AgentLocation source, string folderName, AgentLocation target, Fill fill, CancellationToken cancellationToken)
    {
        var paths = Prepare(source, folderName, target);

        try
        {
            var staging = Stage(paths, folderName, fill, cancellationToken);
            var previous = CopyScratch.NewPrevious(paths.Scratch, folderName);

            if (Directory.Exists(paths.TargetDirectory))
            {
                SetAside(paths.TargetDirectory, previous, staging);
            }

            try
            {
                MoveIn(staging, paths.TargetDirectory);
            }
            catch
            {
                PutBack(previous, paths.TargetDirectory);
                throw;
            }

            TryDelete(previous);
        }
        finally
        {
            TryDeleteIfEmpty(paths.Scratch);
        }
    }

    /// <summary>Parks the old folder. If that fails the old folder has not been touched, only the staging goes.</summary>
    private void SetAside(string targetDirectory, string previous, string staging)
    {
        try
        {
            moveDirectory(targetDirectory, previous);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }
    }

    private void MoveIn(string staging, string targetDirectory)
    {
        try
        {
            moveDirectory(staging, targetDirectory);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }
    }

    /// <summary>
    /// Brings the parked folder back. If even that fails it stays in the scratch directory, which holds the only
    /// copy of the skill until the next start restores it.
    /// </summary>
    private void PutBack(string previous, string targetDirectory) => TryRun(() =>
    {
        if (Directory.Exists(previous))
        {
            moveDirectory(previous, targetDirectory);
        }
    });

    private CopyPaths Prepare(AgentLocation source, string folderName, AgentLocation target)
    {
        SkillFolderName.EnsureIsSingleSegment(folderName);

        var sourceDirectory = Path.Combine(SkillsDirectoryOf(source), folderName);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new SkillCopyFailedException(folderName);
        }

        var targetSkills = SkillsDirectoryOf(target);
        Directory.CreateDirectory(targetSkills);

        return new CopyPaths(sourceDirectory, CopyScratch.BesideSkills(targetSkills), Path.Combine(targetSkills, folderName));
    }

    private string SkillsDirectoryOf(AgentLocation place) => skillsDirectories.For(place.Location, place.Agent);

    private static string Stage(CopyPaths paths, string folderName, Fill fill, CancellationToken cancellationToken)
    {
        var staging = CopyScratch.NewStaging(paths.Scratch, folderName);

        try
        {
            fill(paths.SourceDirectory, staging, cancellationToken);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        return staging;
    }

    private static void TryDelete(string directory) => TryRun(() =>
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    private static void TryDeleteIfEmpty(string directory) => TryRun(() =>
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    });

    /// <summary>Best effort: the skill itself is already in its final state, so a failing cleanup is not an error.</summary>
    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record CopyPaths(string SourceDirectory, string Scratch, string TargetDirectory);
}
