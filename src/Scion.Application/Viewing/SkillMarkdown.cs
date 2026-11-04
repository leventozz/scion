namespace Scion.Application.Viewing;

public static class SkillMarkdown
{
    private const string Delimiter = "---";

    /// <summary>
    /// The text below the frontmatter, using the delimiter rule of the skill reader: the first line is <c>---</c> and
    /// the block ends at the next <c>---</c>. Without a closed block the whole text is the body.
    /// </summary>
    public static string BodyOf(string text)
    {
        var position = 0;
        if (!TryReadLine(text, ref position, out var first) || first != Delimiter)
        {
            return text;
        }

        while (TryReadLine(text, ref position, out var line))
        {
            if (line == Delimiter)
            {
                return text[position..];
            }
        }

        return text;
    }

    private static bool TryReadLine(string text, ref int position, out string line)
    {
        if (position >= text.Length)
        {
            line = "";
            return false;
        }

        var newline = text.IndexOf('\n', position);
        var lineEnd = newline < 0 ? text.Length : newline;
        line = text[position..lineEnd].TrimEnd('\r');
        position = newline < 0 ? text.Length : newline + 1;

        return true;
    }
}
