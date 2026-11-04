namespace Scion.Application.Updating;

public interface IUpdateApplier
{
    /// <summary>
    /// Arranges for a downloaded update to be installed once Scion has exited, and for Scion to start again afterwards.
    /// It does not close Scion: the caller closes the window the normal way.
    /// </summary>
    void ApplyWhenScionExits(AvailableUpdate update);
}
