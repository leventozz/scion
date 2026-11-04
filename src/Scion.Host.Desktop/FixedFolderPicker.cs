using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>Test seam for UI smoke runs: a native dialog cannot be driven from a script.</summary>
internal sealed class FixedFolderPicker(string folder) : IFolderPicker
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(folder);
}
