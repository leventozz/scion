namespace Scion.UI;

public interface IFolderRevealer
{
    /// <summary>Opens a folder in the file manager of the operating system.</summary>
    Task ShowAsync(string folderPath);
}
