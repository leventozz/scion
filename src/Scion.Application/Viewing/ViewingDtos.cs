using Scion.Domain;

namespace Scion.Application.Viewing;

public sealed record SkillFolderListing(string FolderPath, IReadOnlyList<string> FilePaths);

/// <param name="FolderPath">The folder that contains the file, for "Show in folder".</param>
/// <param name="Read">The start of the file, or why it could not be read.</param>
public sealed record SkillFileContent(string FolderPath, Result<FileBytes, FileReadError> Read);

/// <param name="RelativePath">As shown to the user, relative to the location root (<c>.claude/CLAUDE.md</c>).</param>
/// <param name="FolderPath">The folder that contains the file, for "Show in folder".</param>
/// <param name="Read">The start of the file, or why it could not be read.</param>
public sealed record InstructionFileBytes(string RelativePath, string FolderPath, Result<FileBytes, FileReadError> Read);
