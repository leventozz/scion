using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Application.Creating;

public sealed class CreateInstructionFile(IInstructionFileCreator instructionFileCreator)
{
    private static readonly char[] Separators = ['/', '\\'];

    public async Task<Result<EditableFile.InstructionFile, CreateInstructionFileError>> ExecuteAsync(
        Location location, Agent agent, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = await instructionFileCreator.CreateAsync(location, agent, StartingContent(location), cancellationToken);

            return Result<EditableFile.InstructionFile, CreateInstructionFileError>.Success(
                new EditableFile.InstructionFile(location, agent, relativePath));
        }
        catch (InstructionFileCreateFailedException exception)
        {
            return Result<EditableFile.InstructionFile, CreateInstructionFileError>.Failure(exception.Failure switch
            {
                InstructionFileCreateFailure.AlreadyExists => CreateInstructionFileError.AlreadyExists,
                InstructionFileCreateFailure.FolderMissing => CreateInstructionFileError.FolderMissing,
                _ => CreateInstructionFileError.WriteFailed,
            });
        }
    }

    private static string StartingContent(Location location) => location switch
    {
        Location.Project project => $"# {FolderNameOf(project.Path)}\n\n",
        _ => "",
    };

    private static string FolderNameOf(string path)
    {
        var trimmed = path.TrimEnd(Separators);

        return trimmed[(trimmed.LastIndexOfAny(Separators) + 1)..];
    }
}
