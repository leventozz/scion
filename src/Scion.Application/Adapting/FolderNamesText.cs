namespace Scion.Application.Adapting;

internal static class FolderNamesText
{
    public static string Of(IReadOnlyList<string> folderNames) => string.Join('\n', folderNames);
}
