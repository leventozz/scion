using Scion.Application.Skills.Copying;

namespace Scion.Infrastructure.FileSystem;

internal static class SkillFolderName
{
    /// <summary>A skill folder is one path segment of the skills directory; anything else could reach outside it.</summary>
    public static void EnsureIsSingleSegment(string folderName) =>
        EnsureIsSingleSegment(folderName, name => new SkillCopyFailedException(name));

    /// <summary>Same rule; <paramref name="failure"/> makes the exception of the adapter that asks.</summary>
    public static void EnsureIsSingleSegment(string folderName, Func<string, Exception> failure)
    {
        var isSingleSegment = !string.IsNullOrEmpty(folderName)
            && folderName is not ("." or "..")
            && folderName.IndexOfAny(['/', '\\']) < 0;

        if (!isSingleSegment)
        {
            throw failure(folderName);
        }
    }
}
