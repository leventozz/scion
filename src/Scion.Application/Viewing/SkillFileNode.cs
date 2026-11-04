namespace Scion.Application.Viewing;

public abstract record SkillFileNode
{
    private SkillFileNode()
    {
    }

    /// <param name="RelativePath">Relative to the skill folder, <c>/</c>-separated.</param>
    public sealed record File(string Name, string RelativePath) : SkillFileNode;

    public sealed record Folder(string Name, IReadOnlyList<SkillFileNode> Children) : SkillFileNode;
}

public sealed record SkillFileTree(string FolderPath, IReadOnlyList<SkillFileNode> Nodes);
