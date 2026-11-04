namespace Scion.UI;

public interface IExternalLinks
{
    /// <summary>Opens a web address in the default browser. Only http and https addresses are ever passed in.</summary>
    Task OpenAsync(Uri uri);
}
