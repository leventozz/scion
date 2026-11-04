namespace Scion.Application.Updating;

/// <summary>What Scion knows about itself without asking anyone: whether it was installed from a release, and which version it is.</summary>
public interface IScionInstallation
{
    /// <summary>False when Scion was started from source or from a build folder: such a Scion has nothing to update.</summary>
    bool IsInstalled { get; }

    /// <summary>The version of this Scion. For a Scion that is not installed, the version of the build.</summary>
    string Version { get; }
}
