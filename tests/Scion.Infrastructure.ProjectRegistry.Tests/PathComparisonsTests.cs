using System.Runtime.InteropServices;

namespace Scion.Infrastructure.ProjectRegistry.Tests;

public class PathComparisonsTests
{
    [Fact]
    public void Ignores_case_on_windows()
    {
        Assert.Equal(StringComparison.OrdinalIgnoreCase, PathComparisons.For(OSPlatform.Windows));
    }

    [Fact]
    public void Ignores_case_on_macos()
    {
        Assert.Equal(StringComparison.OrdinalIgnoreCase, PathComparisons.For(OSPlatform.OSX));
    }

    [Fact]
    public void Respects_case_on_linux()
    {
        Assert.Equal(StringComparison.Ordinal, PathComparisons.For(OSPlatform.Linux));
    }

    [Fact]
    public void Respects_case_on_any_other_platform()
    {
        Assert.Equal(StringComparison.Ordinal, PathComparisons.For(OSPlatform.FreeBSD));
    }
}
