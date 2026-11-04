namespace Scion.Domain;

public abstract record Location
{
    private Location()
    {
    }

    public sealed record Personal : Location;

    public sealed record Project(string Path) : Location;
}
