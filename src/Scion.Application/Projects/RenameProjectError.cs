namespace Scion.Application.Projects;

public abstract record RenameProjectError
{
    private RenameProjectError()
    {
    }

    public sealed record NameEmpty : RenameProjectError;

    public sealed record NotRegistered : RenameProjectError;

    public sealed record Registry(RegistryFailure Failure) : RenameProjectError;
}
