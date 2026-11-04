namespace Scion.Application.Editing;

/// <summary>A writer adapter failure the Application layer turns into <see cref="SaveFileError.WriteFailed"/>.</summary>
public sealed class FileSaveFailedException(FileSaveFailure failure, Exception? innerException = null)
    : Exception("The file could not be saved.", innerException)
{
    public FileSaveFailure Failure { get; } = failure;
}
