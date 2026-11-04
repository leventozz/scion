namespace Scion.Application.Projects;

public enum RegistryFailureKind
{
    /// <summary>The registry file exists but cannot be read.</summary>
    Unreadable,

    /// <summary>The registry file is readable, but saving a change failed.</summary>
    WriteFailed,
}
