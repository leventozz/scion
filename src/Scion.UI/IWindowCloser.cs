namespace Scion.UI;

/// <summary>Closes the application window. The host implements it; the UI asks for it once the user agreed to lose a draft.</summary>
public interface IWindowCloser
{
    void Close();
}
