using System.Text.RegularExpressions;
using Scion.Application.Adapting;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Sets the <c>name</c> of a SKILL.md by changing the range of that one scalar in the text, so comments, other keys, line
/// endings and the body stay exactly as they were. Without a <c>name</c> key the line is added first in the frontmatter.
/// </summary>
public sealed partial class YamlSkillNameSetter : ISkillNameSetter
{
    private const string Delimiter = "---";

    public string? WithName(string skillFile, string name)
    {
        if (FrontmatterBlock.TryFind(skillFile) is not { } block)
        {
            return null;
        }

        try
        {
            return Replace(skillFile, block, name);
        }
        catch (Exception exception) when (exception is YamlException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Replace(string skillFile, FrontmatterBlock block, string name)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(block.Yaml(skillFile)));

        switch (stream.Documents.Count == 0 ? null : stream.Documents[0].RootNode)
        {
            case null:
                return InsertNameLine(skillFile, block, name);
            case YamlMappingNode mapping:
                return ReplaceOrInsert(skillFile, block, mapping, name);
            default:
                return null;
        }
    }

    /// <summary>A <c>name</c> that is not a scalar (a list, say) is not a name Scion can set: that is no readable header.</summary>
    private static string? ReplaceOrInsert(string skillFile, FrontmatterBlock block, YamlMappingNode mapping, string name)
    {
        var existing = mapping.Children.Where(entry => entry.Key is YamlScalarNode { Value: "name" }).ToList();

        return existing switch
        {
            [] => InsertNameLine(skillFile, block, name),
            [{ Value: YamlScalarNode value }] => ReplaceScalar(skillFile, block, value, name),
            _ => null,
        };
    }

    private static string ReplaceScalar(string skillFile, FrontmatterBlock block, YamlScalarNode value, string name)
    {
        var start = block.YamlStart + (int)value.Start.Index;
        var end = block.YamlStart + (int)value.End.Index;

        // An empty value ("name:") has no text of its own: it goes after the colon, with a space.
        return end == start
            ? skillFile[..start] + " " + Scalar(name) + skillFile[end..]
            : skillFile[..start] + Scalar(name) + skillFile[end..];
    }

    private static string InsertNameLine(string skillFile, FrontmatterBlock block, string name) =>
        skillFile[..block.YamlStart] + $"name: {Scalar(name)}{block.LineEnding}" + skillFile[block.YamlStart..];

    /// <summary>Plain when it reads back as the same text, otherwise double-quoted.</summary>
    private static string Scalar(string name) =>
        PlainName().IsMatch(name) && !ReservedWords.Contains(name.ToLowerInvariant())
            ? name
            : $"\"{name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static readonly HashSet<string> ReservedWords = ["true", "false", "yes", "no", "on", "off", "y", "n", "null", "~"];

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex PlainName();

    /// <summary>Where the YAML between the two <c>---</c> lines starts and ends in the text.</summary>
    private sealed record FrontmatterBlock(int YamlStart, int YamlEnd, string LineEnding)
    {
        public string Yaml(string text) => text[YamlStart..YamlEnd];

        public static FrontmatterBlock? TryFind(string text)
        {
            var position = 0;
            if (!TryReadLine(text, ref position, out var first, out var firstEnding) || first != Delimiter)
            {
                return null;
            }

            var yamlStart = position;
            while (true)
            {
                var lineStart = position;
                if (!TryReadLine(text, ref position, out var line, out _))
                {
                    return null;
                }

                if (line == Delimiter)
                {
                    return new FrontmatterBlock(yamlStart, lineStart, firstEnding);
                }
            }
        }

        private static bool TryReadLine(string text, ref int position, out string line, out string ending)
        {
            line = ending = "";
            if (position >= text.Length)
            {
                return false;
            }

            var newline = text.IndexOf('\n', position);
            var lineEnd = newline < 0 ? text.Length : newline;
            line = text[position..lineEnd].TrimEnd('\r');
            ending = newline < 0 ? "" : text[position..lineEnd].EndsWith('\r') ? "\r\n" : "\n";
            position = newline < 0 ? text.Length : newline + 1;

            return true;
        }
    }
}
