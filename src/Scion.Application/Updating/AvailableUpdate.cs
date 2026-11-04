namespace Scion.Application.Updating;

/// <param name="Version">The version that can be installed.</param>
/// <param name="ReleaseNotes">What the release says is new (Markdown), or null when it says nothing.</param>
public sealed record AvailableUpdate(string Version, string? ReleaseNotes);
