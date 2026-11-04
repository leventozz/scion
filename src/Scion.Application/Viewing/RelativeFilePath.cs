namespace Scion.Application.Viewing;

internal static class RelativeFilePath
{
    /// <summary>
    /// A <c>/</c>-separated path that stays inside its folder: never rooted, no drive letter, no backslash, and no
    /// empty, <c>.</c>, <c>..</c> or dot-prefixed segment (dot entries are hidden everywhere).
    /// </summary>
    public static bool IsSafe(string path)
    {
        if (path.Length == 0 || path.Contains('\\') || HasDriveLetter(path))
        {
            return false;
        }

        return path.Split('/').All(segment => segment.Length > 0 && !segment.StartsWith('.'));
    }

    private static bool HasDriveLetter(string path) => path.Length >= 2 && path[1] == ':';
}
