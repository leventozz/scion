namespace Scion.UI;

/// <summary>The user's home directory and how this platform compares paths. The UI never reads the environment itself.</summary>
public sealed record UserHome(string Directory, StringComparison PathComparison);
