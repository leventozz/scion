namespace Scion.Application.Viewing;

/// <param name="RelativePath">Relative to the location root, as the user knows it (<c>.claude/CLAUDE.md</c>).</param>
/// <param name="FolderPath">The folder that contains the file, for "Show in folder".</param>
public sealed record InstructionFileView(string RelativePath, string FolderPath, InstructionFileContent Content);

public abstract record InstructionFileContent
{
    private InstructionFileContent()
    {
    }

    public sealed record Markdown(string Text) : InstructionFileContent;

    public sealed record NotShown : InstructionFileContent;
}
