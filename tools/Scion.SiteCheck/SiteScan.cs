using System.Text.RegularExpressions;

namespace Scion.SiteCheck;

/// <summary>Reads every page and stylesheet of a site and reports the addresses that lead outside it without being allowed.</summary>
public static partial class SiteScan
{
    private static readonly string[] Extensions = [".html", ".css"];

    public static IReadOnlyList<ExternalReference> Run(string folder, AllowedAddresses allowed)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Site folder not found: {folder}");
        }

        return
        [
            .. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .SelectMany(file => ReferenceFinder.Find(File.ReadAllText(file))
                    .Select(address => address.Trim())
                    .Where(address => LeavesTheSite(address) && !allowed.Allows(address))
                    .Select(address => new ExternalReference(Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), address))),
        ];
    }

    // A scheme (https:, data:, javascript:, mailto:...) or a host without a scheme (// and its backslash spellings) leaves the site.
    private static bool LeavesTheSite(string address) => Scheme().IsMatch(address) || HostWithoutScheme().IsMatch(address);

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9+.-]*:")]
    private static partial Regex Scheme();

    [GeneratedRegex("""^[/\\]{2}""")]
    private static partial Regex HostWithoutScheme();
}
