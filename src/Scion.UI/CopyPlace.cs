using Scion.Domain;

namespace Scion.UI;

/// <summary>A location as the user knows it: "Personal" or a project with its display name.</summary>
public sealed record CopyPlace(string Name, Location Location);
