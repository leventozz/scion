namespace Scion.SiteCheck;

/// <summary>The addresses a page may point to: each listed address and everything below it, nothing that merely starts like one.</summary>
public sealed class AllowedAddresses(IEnumerable<string> addresses)
{
    private static readonly char[] EndsOfAnAddress = ['/', '?', '#'];

    /// <summary>The only external addresses the website may use: Scion's repository and the website itself.</summary>
    public static AllowedAddresses Site { get; } = new(["https://github.com/leventozz/scion", "https://leventozz.github.io/scion"]);

    private readonly string[] _addresses = [.. addresses.Select(address => address.TrimEnd('/'))];

    public bool Allows(string address) => _addresses.Any(allowed => IsOrIsBelow(address, allowed));

    private static bool IsOrIsBelow(string address, string allowed) =>
        address.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
        && (address.Length == allowed.Length || EndsOfAnAddress.Contains(address[allowed.Length]));
}
