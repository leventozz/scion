namespace Scion.Application.Projects;

public sealed record RegistryFailure(RegistryFailureKind Kind, string RegistryFile)
{
    internal static RegistryFailure From(ProjectRegistryException exception) => new(
        exception is ProjectRegistryUnreadableException ? RegistryFailureKind.Unreadable : RegistryFailureKind.WriteFailed,
        exception.RegistryFile);
}
