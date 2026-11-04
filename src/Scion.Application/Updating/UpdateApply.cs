namespace Scion.Application.Updating;

/// <summary>How arranging the installation of a downloaded update ended. A closed set: the UI has a line for each.</summary>
public abstract record UpdateApply
{
    private UpdateApply()
    {
    }

    public sealed record Arranged : UpdateApply;

    /// <param name="Message">What went wrong, as far as it could be told.</param>
    public sealed record Failed(string Message) : UpdateApply;
}
