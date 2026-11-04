using System.Security.Cryptography;

namespace Scion.Application.Editing;

/// <summary>The fingerprint of a file's bytes (lowercase hex SHA-256) that <see cref="EditBaseline.Hash"/> holds.</summary>
public static class ContentHash
{
    public static string Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The same fingerprint of a stream, read in chunks so a large file is never loaded whole.</summary>
    public static async Task<string> OfAsync(Stream stream, CancellationToken cancellationToken) =>
        Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
}
