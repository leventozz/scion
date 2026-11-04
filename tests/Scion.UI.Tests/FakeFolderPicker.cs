namespace Scion.UI.Tests;

internal sealed class FakeFolderPicker : IFolderPicker
{
    public string? Folder { get; set; }

    public int PickCount { get; private set; }

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        PickCount++;
        return Task.FromResult(Folder);
    }
}
