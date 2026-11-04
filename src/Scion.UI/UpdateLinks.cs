namespace Scion.UI;

/// <summary>Where the user can get Scion by hand. The host knows the address; the screens only open it.</summary>
/// <param name="ReleasesPage">The page that lists the releases, for "Download from GitHub instead".</param>
public sealed record UpdateLinks(Uri ReleasesPage);
