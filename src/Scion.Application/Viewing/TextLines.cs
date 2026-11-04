namespace Scion.Application.Viewing;

internal static class TextLines
{
    /// <summary>Lines split on <c>\n</c> or <c>\r\n</c>; a final line terminator does not add an empty line.</summary>
    public static IReadOnlyList<string> Of(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        if (lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }
}
