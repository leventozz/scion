namespace Scion.Application.Creating;

/// <summary>A creator adapter failure the Application layer turns into <see cref="CreateInstructionFileError"/>.</summary>
public sealed class InstructionFileCreateFailedException(InstructionFileCreateFailure failure, Exception? innerException = null)
    : Exception("The instruction file could not be created.", innerException)
{
    public InstructionFileCreateFailure Failure { get; } = failure;
}
