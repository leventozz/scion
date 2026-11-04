namespace Scion.Application.Adapting;

/// <summary>
/// One line of the short "What will be shared" summary. Every <see cref="SharedItem"/> is in the summary exactly once:
/// as an <see cref="Item"/> of its own, or counted in <see cref="ProjectFiles"/>.
/// </summary>
public abstract record SharedSummaryLine
{
    private SharedSummaryLine()
    {
    }

    /// <summary>A shared item that has a line of its own.</summary>
    public sealed record Item(SharedItem Value) : SharedSummaryLine;

    /// <summary>The files read from a project (instruction files and manifests), counted as one line.</summary>
    public sealed record ProjectFiles(int Count, int Bytes) : SharedSummaryLine;
}
