namespace Scion.Application.Creating;

/// <summary>A creator adapter failure the Application layer turns into <see cref="CreateSkillError"/>.</summary>
public sealed class SkillCreateFailedException(SkillCreateFailure failure, Exception? innerException = null)
    : Exception("The skill could not be created.", innerException)
{
    public SkillCreateFailure Failure { get; } = failure;
}
