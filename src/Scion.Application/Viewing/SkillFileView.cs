namespace Scion.Application.Viewing;

public abstract record SkillFileView
{
    private SkillFileView()
    {
    }

    /// <summary>The root SKILL.md without its frontmatter.</summary>
    public sealed record Markdown(string Body) : SkillFileView;

    public sealed record PlainText(IReadOnlyList<string> Lines) : SkillFileView;

    /// <param name="FolderPath">The folder that contains the file, for "Show in folder".</param>
    public sealed record NotShown(string FolderPath) : SkillFileView;
}
