namespace Scion.SiteCheck;

/// <summary>An address in a file of the site that leads outside the site and is not allowed.</summary>
/// <param name="File">The file, relative to the site's folder, with forward slashes.</param>
public sealed record ExternalReference(string File, string Address);
