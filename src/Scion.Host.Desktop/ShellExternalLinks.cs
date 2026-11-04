using System.ComponentModel;
using System.Diagnostics;
using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>
/// Opens a web address in the default browser. The UI only passes http(s) addresses, and this checks again,
/// because the shell would happily start any file or protocol handler.
/// </summary>
internal sealed class ShellExternalLinks : IExternalLinks
{
    public Task OpenAsync(Uri uri)
    {
        if (uri.IsAbsoluteUri && uri.Scheme is "http" or "https")
        {
            try
            {
                using var _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                // No browser is registered; there is nothing sensible to show for that.
            }
        }

        return Task.CompletedTask;
    }
}
