namespace Scion.Application.Projects;

public sealed class ProjectRegistryUnreadableException(string registryFile, Exception? innerException = null)
    : ProjectRegistryException(registryFile, $"The project registry file '{registryFile}' could not be read.", innerException);
