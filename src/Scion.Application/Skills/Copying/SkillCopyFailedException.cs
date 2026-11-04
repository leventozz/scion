namespace Scion.Application.Skills.Copying;

/// <summary>A copier adapter failure the Application layer turns into <see cref="SkillCopyOutcome.Failed"/>.</summary>
public sealed class SkillCopyFailedException(string folderName, Exception? innerException = null)
    : Exception($"The skill folder '{folderName}' could not be copied.", innerException)
{
    public string FolderName { get; } = folderName;
}
