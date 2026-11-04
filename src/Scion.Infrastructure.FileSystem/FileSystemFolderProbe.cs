using Scion.Application.Projects;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemFolderProbe : IFolderProbe
{
    public bool Exists(string path) => Directory.Exists(path);
}
