namespace Scion.Application.Projects;

public abstract record OpenProjectError
{
    private OpenProjectError()
    {
    }

    public sealed record NotRegistered : OpenProjectError;

    public sealed record Registry(RegistryFailure Failure) : OpenProjectError;
}
