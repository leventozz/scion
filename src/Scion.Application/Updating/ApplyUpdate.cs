namespace Scion.Application.Updating;

/// <summary>Installs a downloaded update when Scion exits. Closing Scion is the caller's step, after the user agreed to leave.</summary>
public sealed class ApplyUpdate(IUpdateApplier applier)
{
    public UpdateApply Execute(AvailableUpdate update)
    {
        try
        {
            applier.ApplyWhenScionExits(update);

            return new UpdateApply.Arranged();
        }
        catch (UpdateFailedException exception)
        {
            return new UpdateApply.Failed(exception.Message);
        }
    }
}
