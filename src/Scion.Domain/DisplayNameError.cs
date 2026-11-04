namespace Scion.Domain;

public abstract record DisplayNameError
{
    private DisplayNameError()
    {
    }

    public sealed record Empty : DisplayNameError;
}
