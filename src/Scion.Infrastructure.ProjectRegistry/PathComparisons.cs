using System.Runtime.InteropServices;

namespace Scion.Infrastructure.ProjectRegistry;

public static class PathComparisons
{
    /// <summary>The default file systems of Windows and macOS ignore case; everything else respects it.</summary>
    public static StringComparison For(OSPlatform platform) =>
        platform == OSPlatform.Windows || platform == OSPlatform.OSX
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
