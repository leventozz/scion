namespace Scion.Application.Updating;

/// <summary>What the last successful check for updates left behind.</summary>
/// <param name="CheckedAt">When Scion last asked the feed.</param>
/// <param name="CheckedOnVersion">The version of Scion that asked: an update it found says nothing to another version.</param>
/// <param name="Found">The newer version it found, or null when it found none.</param>
public sealed record LastUpdateCheck(DateTimeOffset CheckedAt, string CheckedOnVersion, AvailableUpdate? Found);
