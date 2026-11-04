namespace Scion.Application.Editing;

public abstract record SaveFileError
{
    private SaveFileError()
    {
    }

    public sealed record NotInSkillFolder : SaveFileError;

    public sealed record DeletedOnDisk : SaveFileError;

    public sealed record ChangedOnDisk : SaveFileError;

    public sealed record Unreadable : SaveFileError;

    public sealed record WriteFailed(FileSaveFailure Failure) : SaveFileError;
}
