using Scion.Application.Editing;

namespace Scion.Application.Tests.Editing;

/// <summary>Reads <c>name</c> and <c>description</c> from lines like <c>name: x</c>; an <c>UNREADABLE</c> line breaks it.</summary>
internal sealed class FakeFrontmatterParser : ISkillFrontmatterParser
{
    public SkillFrontmatterFields Parse(string text)
    {
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
