using Scion.Application.Adapting;

namespace Scion.UI.Tests;

/// <summary>Replaces a <c>name: x</c> line; a text with an <c>UNREADABLE</c> line has no frontmatter to change.</summary>
internal sealed class FakeSkillNameSetter : ISkillNameSetter
{
    public string? WithName(string skillFile, string name)
    {
        var lines = skillFile.Split('\n');

        return lines.Contains("UNREADABLE")
            ? null
            : string.Join('\n', lines.Select(line => line.StartsWith("name: ") ? $"name: {name}" : line));
    }
}
