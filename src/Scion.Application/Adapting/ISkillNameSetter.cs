namespace Scion.Application.Adapting;

public interface ISkillNameSetter
{
    /// <summary>
    /// The SKILL.md text with its <c>name</c> set to <paramref name="name"/> and nothing else changed. A text without
    /// readable frontmatter gives <c>null</c>. It does no I/O.
    /// </summary>
    string? WithName(string skillFile, string name);
}
