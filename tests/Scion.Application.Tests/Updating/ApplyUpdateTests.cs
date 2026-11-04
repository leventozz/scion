using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class ApplyUpdateTests
{
    private readonly RecordingUpdateApplier _applier = new();

    [Fact]
    public void Arranges_for_the_downloaded_update_to_be_installed_when_scion_exits()
    {
        var update = new AvailableUpdate("0.2.0", "Faster start.");

        var apply = new ApplyUpdate(_applier).Execute(update);

        Assert.Equal(new UpdateApply.Arranged(), apply);
        Assert.Equal([update], _applier.Applied);
    }

    [Fact]
    public void An_update_that_cannot_be_arranged_says_why()
    {
        _applier.Failure = new UpdateFailedException("The package is gone.");

        var apply = new ApplyUpdate(_applier).Execute(new AvailableUpdate("0.2.0", null));

        Assert.Equal(new UpdateApply.Failed("The package is gone."), apply);
    }
}
