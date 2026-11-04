using System.Text;

namespace Scion.UI;

internal static class AvatarStyle
{
    private static readonly char[] WordSeparators = ['-', '_', '.', ' '];

    public static string Initials(string name)
    {
        var words = name.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => "",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{words[0][0]}{words[1][0]}".ToUpperInvariant(),
        };
    }

    /// <summary>Palette entry at FNV-1a(name) modulo the palette size. Never string.GetHashCode: that changes between runs.</summary>
    public static AvatarTone Tone(string name)
    {
        var hash = 2166136261u;

        foreach (var value in Encoding.UTF8.GetBytes(name))
        {
            hash = (hash ^ value) * 16777619u;
        }

        return (AvatarTone)(hash % (uint)Enum.GetValues<AvatarTone>().Length);
    }
}
