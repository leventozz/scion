namespace Scion.Application.Projects;

public interface IFolderProbe
{
    bool Exists(string path);
}
