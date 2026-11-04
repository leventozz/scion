namespace Scion.Application.Adapting;

public enum DiffKind
{
    Same,
    Removed,
    Added,
}

/// <param name="Number">The 1-based line number in its own text.</param>
public sealed record DiffLine(int Number, string Text, DiffKind Kind);

/// <summary>One row of a side-by-side diff. A side is null where the other side has a line that this one has not.</summary>
public sealed record DiffRow(DiffLine? Left, DiffLine? Right);
