using Photino.NET;
using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>The window only exists after the app is built, so it is handed over lazily.</summary>
internal sealed class PhotinoFolderPicker(Func<PhotinoWindow> mainWindow, string startDirectory) : IFolderPicker
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        var folders = await mainWindow().ShowOpenFolderAsync("Add project", startDirectory, multiSelect: false);

        return folders is { Length: > 0 } ? folders[0] : null;
    }
}
