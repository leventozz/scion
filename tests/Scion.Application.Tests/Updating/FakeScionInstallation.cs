using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class FakeScionInstallation : IScionInstallation
{
    public bool IsInstalled { get; set; } = true;

    public string Version { get; set; } = "0.1.0";
}
