namespace Scion.Application.Adapting;

/// <summary>The labels of shared items that are not files, in one place: the shared list and the not-shared list must agree.</summary>
internal static class SharedItemLabels
{
    public static string FolderNames(string targetName) => $"Folder names in {targetName} (two levels)";
}
