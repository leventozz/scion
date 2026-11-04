using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scion.Infrastructure.FileSystem;

internal static class SkillFrontmatterReader
{
    private const string Delimiter = "---";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static async Task<SkillFrontmatter> ReadAsync(string skillFilePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(skillFilePath);

        return await Task.Run(() => Read(reader), cancellationToken);
    }

    /// <summary>Reads line by line and stops at the closing delimiter, so only the header is ever read.</summary>
    public static SkillFrontmatter Read(TextReader reader)
    {
        if (reader.ReadLine() != Delimiter)
        {
            return SkillFrontmatter.Absent;
        }

        var block = new StringBuilder();
        var closed = false;
        while (!closed && reader.ReadLine() is { } line)
        {
            closed = line == Delimiter;
            if (!closed)
            {
                block.AppendLine(line);
            }
        }

        return closed ? Parse(block.ToString()) : SkillFrontmatter.Unreadable;
    }

    private static SkillFrontmatter Parse(string yaml)
    {
        try
        {
            var fields = Deserializer.Deserialize<Fields?>(yaml) ?? new Fields();
            return new SkillFrontmatter(fields.Name, fields.Description);
        }
        catch (YamlException)
        {
            return SkillFrontmatter.Unreadable;
        }
    }

    private sealed class Fields
    {
        public string? Name { get; set; }

        public string? Description { get; set; }
    }
}

internal sealed record SkillFrontmatter(string? Name, string? Description, bool IsUnreadable = false)
{
    public static readonly SkillFrontmatter Absent = new(null, null);

    public static readonly SkillFrontmatter Unreadable = new(null, null, IsUnreadable: true);
}
