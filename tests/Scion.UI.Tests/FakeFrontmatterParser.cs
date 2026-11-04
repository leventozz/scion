using Scion.Application.Editing;

namespace Scion.UI.Tests;

/// <summary>Reads <c>name</c> and <c>description</c> from lines like <c>name: x</c>; an <c>UNREADABLE</c> line breaks it.</summary>
internal sealed class FakeFrontmatterParser : ISkillFrontmatterParser
{
    /// <summary>How many times a draft was checked.</summary>
    public int Calls { get; private set; }

    public SkillFrontmatterFields Parse(string text)
    {
        Calls++;
        string? name = null;
        string? description = null;

        foreach (var line in text.Split('\n'))
        {
            if (line == "UNREADABLE")
            {
                return new SkillFrontmatterFields(null, null, IsUnreadable: true);
            }

            if (line.StartsWith("name: "))
            {
                name = line["name: ".Length..];
            }
            else if (line.StartsWith("description: "))
            {
                description = line["description: ".Length..];
            }
        }

        return new SkillFrontmatterFields(name, description, IsUnreadable: false);
    }
}
