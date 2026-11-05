using System.Text.RegularExpressions;

namespace Scion.SiteCheck;

/// <summary>Finds the addresses a page or a stylesheet asks the browser to load or follow.</summary>
public static partial class ReferenceFinder
{
    public static IReadOnlyList<string> Find(string text) =>
        [.. FromAttributes(text), .. FromStylesheets(text)];

    private static IEnumerable<string> FromAttributes(string text)
    {
        foreach (Match attribute in Attribute().Matches(text))
        {
            var value = attribute.Groups["double"].Success ? attribute.Groups["double"].Value
                : attribute.Groups["single"].Success ? attribute.Groups["single"].Value
                : attribute.Groups["bare"].Value;

            var addresses = attribute.Groups["name"].Value.Equals("srcset", StringComparison.OrdinalIgnoreCase)
                ? SourceSet(value)
                : [value];

            foreach (var address in addresses)
            {
                yield return address;
            }
        }
    }

    private static IEnumerable<string> FromStylesheets(string text) =>
        CssUrl().Matches(text).Select(match => match.Groups["address"].Value)
            .Concat(CssImport().Matches(text).Select(match => match.Groups["address"].Value));

    // The candidates of a srcset, read the way the HTML standard does: an address runs to the next white space,
    // so a comma inside a data address stays; trailing commas end the candidate, otherwise the descriptor runs to the next comma.
    private static IEnumerable<string> SourceSet(string value)
    {
        var position = 0;
        while (position < value.Length)
        {
            while (position < value.Length && (char.IsWhiteSpace(value[position]) || value[position] == ','))
            {
                position++;
            }

            var start = position;
            while (position < value.Length && !char.IsWhiteSpace(value[position]))
            {
                position++;
            }

            var address = value[start..position];
            if (address.Length == 0)
            {
                yield break;
            }

            if (address.EndsWith(','))
            {
                address = address.TrimEnd(',');
            }
            else
            {
                while (position < value.Length && value[position] != ',')
                {
                    position++;
                }
            }

            yield return address;
        }
    }

    [GeneratedRegex("""(?<![\w-])(?<name>src|href|srcset)\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<bare>[^\s"'>]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex Attribute();

    [GeneratedRegex("""url\(\s*(?<quote>["']?)(?<address>.*?)\k<quote>\s*\)""", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrl();

    [GeneratedRegex("""@import\s+(?<quote>["'])(?<address>.*?)\k<quote>""", RegexOptions.IgnoreCase)]
    private static partial Regex CssImport();
}
