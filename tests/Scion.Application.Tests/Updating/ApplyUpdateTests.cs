using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class ApplyUpdateTests
{
    [Fact]
    public void Arranges_for_the_downloaded_update_to_be_installed_when_scion_exits()
    {
        var applier = new RecordingUpdateApplier();
        var update = new AvailableUpdate("0.2.0", "Faster start.");

        new ApplyUpdate(applier).Execute(update);

        Assert.Equal([update], applier.Applied);
    }
}
