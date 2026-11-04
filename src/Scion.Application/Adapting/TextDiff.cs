using Scion.Application.Editing;

namespace Scion.Application.Adapting;

/// <summary>A line diff of two texts for the review: the original on the left, the adapted text on the right.</summary>
public static class TextDiff
{
    public static IReadOnlyList<DiffRow> SideBySide(string original, string adapted)
    {
        var originalLines = LinesOf(original);
        var adaptedLines = LinesOf(adapted);

        var rows = new List<DiffRow>();
        var removed = new List<DiffLine>();
        var added = new List<DiffLine>();

        foreach (var edit in LineEdits.Between(originalLines, adaptedLines))
        {
            switch (edit.Kind)
            {
                case DiffKind.Removed:
                    removed.Add(new DiffLine(edit.OriginalIndex + 1, originalLines[edit.OriginalIndex], DiffKind.Removed));
                    break;
                case DiffKind.Added:
                    added.Add(new DiffLine(edit.AdaptedIndex + 1, adaptedLines[edit.AdaptedIndex], DiffKind.Added));
                    break;
                default:
                    PairUp(rows, removed, added);
                    rows.Add(new DiffRow(
                        new DiffLine(edit.OriginalIndex + 1, originalLines[edit.OriginalIndex], DiffKind.Same),
                        new DiffLine(edit.AdaptedIndex + 1, adaptedLines[edit.AdaptedIndex], DiffKind.Same)));
                    break;
            }
        }

        PairUp(rows, removed, added);

        return rows;
    }

    /// <summary>A changed block shows its removed and added lines side by side, row by row; the longer side goes on alone.</summary>
    private static void PairUp(List<DiffRow> rows, List<DiffLine> removed, List<DiffLine> added)
    {
        for (var index = 0; index < Math.Max(removed.Count, added.Count); index++)
        {
            rows.Add(new DiffRow(
                index < removed.Count ? removed[index] : null,
                index < added.Count ? added[index] : null));
        }

        removed.Clear();
        added.Clear();
    }

    private static string[] LinesOf(string text)
    {
        var normalized = LineBreaks.Normalize(text);
        if (normalized.Length == 0)
        {
            return [];
        }

        var lines = normalized.Split('\n');

        return normalized.EndsWith('\n') ? lines[..^1] : lines;
    }
}
