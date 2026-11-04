using System.Text.RegularExpressions;

namespace Scion.Application.Creating;

/// <summary>The description of a new skill is one line: line breaks become spaces, and the edges are trimmed.</summary>
public static partial class NewSkillDescription
{
    /// <summary>For while the user types or pastes: the edges stay, so a space at the end can be followed by a word.</summary>
    public static string SingleLine(string text) => LineBreak().Replace(text, " ");

    public static string Normalize(string text) => SingleLine(text).Trim();

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreak();
}
