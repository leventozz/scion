using System.ComponentModel;
using System.Diagnostics;
using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>Opens a folder in the file manager of the operating system (Explorer, Finder, xdg-open).</summary>
internal sealed class ShellFolderRevealer : IFolderRevealer
{
    public Task ShowAsync(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            try
            {
                using var _ = Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                // No file manager is registered; there is nothing sensible to show for that.
            }
        }

        return Task.CompletedTask;
    }
}
