namespace Scion.UI;

internal static class DisplayPath
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>Shows the home directory as "~". A sibling such as "/home/meow" is not inside "/home/me".</summary>
    public static string Abbreviate(string path, UserHome home)
    {
        var directory = home.Directory.TrimEnd(Separators);

        if (path.Equals(directory, home.PathComparison))
        {
            return "~";
        }

        var isInsideHome = path.Length > directory.Length
            && Array.IndexOf(Separators, path[directory.Length]) >= 0
            && path.StartsWith(directory, home.PathComparison);

        return isInsideHome ? "~" + path[directory.Length..] : path;
    }

    /// <summary>The head keeps its trailing separator, so head + last folder is the whole path.</summary>
    public static (string Head, string LastFolder) Split(string displayPath)
    {
        var lastSeparator = displayPath.LastIndexOfAny(Separators);

        return (displayPath[..(lastSeparator + 1)], displayPath[(lastSeparator + 1)..]);
    }
}
