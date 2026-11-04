namespace Scion.Application.Adapting;

public enum SaveAdaptedSkillError
{
    /// <summary>The target has the skill and the user did not choose to overwrite it.</summary>
    AlreadyExists,
    TargetFolderMissing,
    Failed,
}
