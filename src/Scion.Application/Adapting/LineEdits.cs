namespace Scion.Application.Adapting;

/// <summary>One step of turning the first list of lines into the second: keep a line, drop one, or add one.</summary>
internal readonly record struct LineEdit(DiffKind Kind, int OriginalIndex, int AdaptedIndex);

/// <summary>
/// The shortest edit script between two lists of lines (Myers' O(ND) algorithm). When the texts differ by more than
/// <see cref="MaxEdits"/> lines the script is "drop everything, add everything": a review of that many changes is a
/// rewrite anyway, and the memory the trace needs grows with the square of the edit count.
/// </summary>
internal static class LineEdits
{
    private const int MaxEdits = 1000;

    public static IReadOnlyList<LineEdit> Between(IReadOnlyList<string> original, IReadOnlyList<string> adapted)
    {
        var prefix = CommonPrefix(original, adapted);
        var suffix = CommonSuffix(original, adapted, prefix);
        var middleOriginal = original.Skip(prefix).Take(original.Count - prefix - suffix).ToArray();
        var middleAdapted = adapted.Skip(prefix).Take(adapted.Count - prefix - suffix).ToArray();

        var edits = new List<LineEdit>();
        edits.AddRange(Enumerable.Range(0, prefix).Select(index => new LineEdit(DiffKind.Same, index, index)));
        edits.AddRange(EditsOfMiddle(middleOriginal, middleAdapted)
            .Select(edit => edit with { OriginalIndex = edit.OriginalIndex + prefix, AdaptedIndex = edit.AdaptedIndex + prefix }));
        edits.AddRange(Enumerable.Range(0, suffix).Select(index => new LineEdit(
            DiffKind.Same, original.Count - suffix + index, adapted.Count - suffix + index)));

        return edits;
    }

    private static int CommonPrefix(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        var length = 0;
        while (length < first.Count && length < second.Count && first[length] == second[length])
        {
            length++;
        }

        return length;
    }

    private static int CommonSuffix(IReadOnlyList<string> first, IReadOnlyList<string> second, int prefix)
    {
        var length = 0;
        while (length < first.Count - prefix && length < second.Count - prefix
            && first[^(length + 1)] == second[^(length + 1)])
        {
            length++;
        }

        return length;
    }

    private static List<LineEdit> EditsOfMiddle(string[] original, string[] adapted)
    {
        if (original.Length == 0 || adapted.Length == 0)
        {
            return DropAllThenAddAll(original.Length, adapted.Length);
        }

        return Myers(original, adapted) ?? DropAllThenAddAll(original.Length, adapted.Length);
    }

    private static List<LineEdit> DropAllThenAddAll(int originalCount, int adaptedCount) =>
    [
        .. Enumerable.Range(0, originalCount).Select(index => new LineEdit(DiffKind.Removed, index, -1)),
        .. Enumerable.Range(0, adaptedCount).Select(index => new LineEdit(DiffKind.Added, -1, index)),
    ];

    /// <summary>The shortest script, or null when it needs more than <see cref="MaxEdits"/> edits.</summary>
    private static List<LineEdit>? Myers(string[] original, string[] adapted)
    {
        var limit = Math.Min(original.Length + adapted.Length, MaxEdits);
        var offset = limit + 1;
        var furthest = new int[2 * limit + 3];
        var trace = new List<int[]>();

        for (var edits = 0; edits <= limit; edits++)
        {
            trace.Add((int[])furthest.Clone());

            for (var diagonal = -edits; diagonal <= edits; diagonal += 2)
            {
                var goesDown = diagonal == -edits
                    || (diagonal != edits && furthest[offset + diagonal - 1] < furthest[offset + diagonal + 1]);
                var x = goesDown ? furthest[offset + diagonal + 1] : furthest[offset + diagonal - 1] + 1;
                var y = x - diagonal;

                while (x < original.Length && y < adapted.Length && original[x] == adapted[y])
                {
                    x++;
                    y++;
                }

                furthest[offset + diagonal] = x;

                if (x >= original.Length && y >= adapted.Length)
                {
                    return Backtrack(trace, original.Length, adapted.Length, offset);
                }
            }
        }

        return null;
    }

    private static List<LineEdit> Backtrack(List<int[]> trace, int originalLength, int adaptedLength, int offset)
    {
        var edits = new List<LineEdit>();
        int x = originalLength, y = adaptedLength;

        for (var step = trace.Count - 1; step > 0; step--)
        {
            var before = trace[step];
            var diagonal = x - y;
            var cameFromAbove = diagonal == -step
                || (diagonal != step && before[offset + diagonal - 1] < before[offset + diagonal + 1]);
            var previousDiagonal = cameFromAbove ? diagonal + 1 : diagonal - 1;
            var previousX = before[offset + previousDiagonal];
            var previousY = previousX - previousDiagonal;

            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                edits.Add(new LineEdit(DiffKind.Same, x, y));
            }

            edits.Add(x == previousX
                ? new LineEdit(DiffKind.Added, -1, --y)
                : new LineEdit(DiffKind.Removed, --x, -1));
        }

        while (x > 0 && y > 0)
        {
            x--;
            y--;
            edits.Add(new LineEdit(DiffKind.Same, x, y));
        }

        edits.Reverse();

        return edits;
    }
}
