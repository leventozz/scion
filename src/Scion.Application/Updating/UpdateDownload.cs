namespace Scion.Application.Updating;

/// <summary>How downloading an update ended. A closed set: the UI has a line for each.</summary>
public abstract record UpdateDownload
{
    private UpdateDownload()
    {
    }

    public sealed record Downloaded : UpdateDownload;

    public sealed record Cancelled : UpdateDownload;

    /// <param name="Message">What went wrong, as far as it could be told.</param>
    public sealed record Failed(string Message) : UpdateDownload;
}
