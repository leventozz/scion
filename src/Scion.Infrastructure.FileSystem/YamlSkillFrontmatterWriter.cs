using Scion.Application.Creating;
using Scion.Domain.Skills;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scion.Infrastructure.FileSystem;

/// <summary>Writes the header of a new SKILL.md; YamlDotNet quotes a value exactly when plain YAML would misread it.</summary>
public sealed class YamlSkillFrontmatterWriter : ISkillFrontmatterWriter
{
    private const string Delimiter = "---";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithQuotingNecessaryStrings()
        .Build();

    public string Write(SkillName name, SkillDescription description)
    {
        // The emitter writes the line ending of the platform; a SKILL.md made by Scion always has LF.
        var yaml = Serializer.Serialize(new Fields(name.Value, description.Value)).ReplaceLineEndings("\n").TrimEnd('\n');

        return $"{Delimiter}\n{yaml}\n{Delimiter}\n";
    }

    private sealed record Fields(string Name, string Description);
}
