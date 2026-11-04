using Velopack;
using Velopack.Locators;

namespace Scion.Infrastructure.Velopack.Tests;

/// <summary>Where Velopack thinks Scion is installed. Velopack's own test locator, with the two things the tests need besides.</summary>
internal static class Locators
{
    public static IVelopackLocator Installed(string version, string packagesFolder) =>
        new TestVelopackLocator("Scion", version, packagesFolder);

    public static IVelopackLocator NotInstalled(string packagesFolder) => new NotInstalledLocator(packagesFolder);

    private sealed class NotInstalledLocator(string packagesFolder) : TestVelopackLocator("Scion", "0.0.0", packagesFolder)
    {
        public override SemanticVersion? CurrentlyInstalledVersion => null;
    }
}

/// <summary>An installed Scion whose updater is a file that does nothing, and that remembers what it was asked to run.</summary>
internal sealed class RecordingLocator : TestVelopackLocator, IProcessImpl
{
    private RecordingLocator(string folder)
        : base("Scion", "0.1.0", folder, folder, folder, Path.Combine(folder, "Update.exe"), "win", null!, null!, null!)
    {
    }

    public List<(string Program, IReadOnlyList<string> Arguments)> Started { get; } = [];

    public static RecordingLocator In(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "Update.exe"), "");

        return new RecordingLocator(folder);
    }

    void IProcessImpl.StartProcess(string exePath, IEnumerable<string> args, string workDir, bool showWindow) =>
        Started.Add((exePath, args.ToList()));
}
