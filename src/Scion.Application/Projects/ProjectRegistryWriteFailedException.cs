namespace Scion.Application.Projects;

public sealed class ProjectRegistryWriteFailedException(string registryFile, Exception? innerException = null)
    : ProjectRegistryException(registryFile, $"The project registry file '{registryFile}' could not be written.", innerException);
