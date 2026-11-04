using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>Replaces a <c>name: x</c> line; a text with an <c>UNREADABLE</c> line has no frontmatter to change.</summary>
internal sealed class FakeSkillNameSetter : ISkillNameSetter
{
    public string? WithName(string skillFile, string name)
    {
        var lines = skillFile.Split('\n');
        if (lines.Contains("UNREADABLE"))
        {
            return null;
        }

        return string.Join('\n', lines.Select(line => line.StartsWith("name: ") ? $"name: {name}" : line));
    }
}
