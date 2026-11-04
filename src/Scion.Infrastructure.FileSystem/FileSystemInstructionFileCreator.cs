using System.Text;
using Scion.Application.Creating;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Creates the instruction file an agent reads first in a location. The content is staged in a temp file beside the
/// target and moved into place without replacing anything, so a file that is already there is never touched.
/// </summary>
public sealed class FileSystemInstructionFileCreator : IInstructionFileCreator
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _homeDirectory;
    private readonly IReadOnlyList<IInstructionFileLayout> _layouts;
    private readonly Action<string, string> _moveFile;

    public FileSystemInstructionFileCreator(string homeDirectory, IEnumerable<IInstructionFileLayout> layouts)
        : this(homeDirectory, layouts, (from, to) => File.Move(from, to, overwrite: false))
    {
    }

    /// <param name="moveFile">The one step that tests replace to make a move fail. It must not replace an existing target.</param>
    internal FileSystemInstructionFileCreator(
        string homeDirectory, IEnumerable<IInstructionFileLayout> layouts, Action<string, string> moveFile)
    {
        _homeDirectory = homeDirectory;
        _layouts = [.. layouts];
        _moveFile = moveFile;
    }

    public async Task<string> CreateAsync(
        Location location, Agent agent, string content, CancellationToken cancellationToken)
    {
        var layout = _layouts.Single(candidate => candidate.Agent == agent);
        var conventionPaths = location is Location.Personal ? layout.PersonalInstructionFiles : layout.ProjectInstructionFiles;
        var root = LocationRoot.Of(location, _homeDirectory);

        if (!Directory.Exists(root) && location is Location.Project)
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.FolderMissing);
        }

        if (conventionPaths.Any(relativePath => File.Exists(PathUnder(root, relativePath))))
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.AlreadyExists);
        }

        var target = PathUnder(root, conventionPaths[0]);
        await WriteAsync(target, content, cancellationToken);

        return conventionPaths[0];
    }

    private async Task WriteAsync(string target, string content, CancellationToken cancellationToken)
    {
        var staging = InstructionTempFile.NewPathBeside(target);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(staging, content, Utf8WithoutBom, cancellationToken);
            MoveIn(staging, target);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.AccessDenied, exception);
        }
        catch (IOException exception)
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.Other, exception);
        }
        finally
        {
            BestEffort.DeleteFile(staging);
        }
    }

    // The move refuses to replace a file. Whatever made it fail, a file that is there now was not made by us.
    private void MoveIn(string staging, string target)
    {
        try
        {
            _moveFile(staging, target);
        }
        catch (IOException) when (File.Exists(target))
        {
            throw new InstructionFileCreateFailedException(InstructionFileCreateFailure.AlreadyExists);
        }
    }

    private static string PathUnder(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
