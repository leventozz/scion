using Scion.Domain;

namespace Scion.Application.Editing;

/// <summary>Addresses one file Scion can edit, without saying where it lives on disk.</summary>
public abstract record EditableFile
{
    private EditableFile()
    {
    }

    /// <param name="RelativePath">A <c>/</c>-separated path inside the skill folder (<c>scripts/run.sh</c>).</param>
    public sealed record SkillFile(Location Location, Agent Agent, string FolderName, string RelativePath) : EditableFile;

    /// <param name="RelativePath">Relative to the location root, as the user knows it (<c>.claude/CLAUDE.md</c>).</param>
    public sealed record InstructionFile(Location Location, Agent Agent, string RelativePath) : EditableFile;
}
