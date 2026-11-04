namespace Scion.Application.Adapting;

/// <summary>What a reader found about a target project: allowlisted files and the names of its folders.</summary>
public sealed record TargetContext(IReadOnlyList<TargetContextFile> Files, IReadOnlyList<string> FolderNames);

/// <param name="RelativePath">Relative to the location root, <c>/</c>-separated.</param>
/// <param name="Bytes">At most <c>MaxFileBytes + 1</c> bytes: enough to tell that the file is over the limit.</param>
public sealed record TargetContextFile(string RelativePath, TargetContextFileKind Kind, byte[] Bytes);

public enum TargetContextFileKind
{
    InstructionFile,
    Manifest,
}
