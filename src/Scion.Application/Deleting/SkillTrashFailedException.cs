namespace Scion.Application.Deleting;

/// <summary>A trash adapter failure the Application layer turns into <see cref="DeleteSkillError"/>.</summary>
public sealed class SkillTrashFailedException(SkillTrashFailure failure, Exception? innerException = null)
    : Exception("The skill could not be moved to the trash.", innerException)
{
    public SkillTrashFailure Failure { get; } = failure;
}
