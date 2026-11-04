namespace Scion.Application.Editing;

internal static class LineBreaks
{
    /// <summary>Turns <c>\r\n</c> and a lone <c>\r</c> into <c>\n</c>.</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
