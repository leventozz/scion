namespace Scion.Application.Projects;

/// <summary>A registry adapter failure the Application layer turns into a <see cref="RegistryFailure"/> value.</summary>
public abstract class ProjectRegistryException : Exception
{
    protected ProjectRegistryException(string registryFile, string message, Exception? innerException)
        : base(message, innerException)
    {
        RegistryFile = registryFile;
    }

    public string RegistryFile { get; }
}
