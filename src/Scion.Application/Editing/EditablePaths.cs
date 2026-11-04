using Scion.Application.Viewing;

namespace Scion.Application.Editing;

internal static class EditablePaths
{
    /// <summary>A skill file path must stay inside its skill folder; an instruction file is addressed by convention.</summary>
    public static bool IsSafe(EditableFile file) =>
        file is not EditableFile.SkillFile skillFile || RelativeFilePath.IsSafe(skillFile.RelativePath);
}
