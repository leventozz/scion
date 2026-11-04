using Photino.NET;
using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>Closes the PhotinoX window; the UI asks for it once the user agreed to lose an unsaved draft.</summary>
internal sealed class PhotinoWindowCloser(Func<PhotinoWindow> mainWindow) : IWindowCloser
{
    public void Close() => mainWindow().Close();
}
