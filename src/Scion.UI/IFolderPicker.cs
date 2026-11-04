namespace Scion.UI;

public interface IFolderPicker
{
    /// <summary>Asks the user for a folder. Returns <c>null</c> when the user cancels.</summary>
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}
