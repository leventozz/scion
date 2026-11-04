using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class RecordingUpdateApplier : IUpdateApplier
{
    public List<AvailableUpdate> Applied { get; } = [];

    public void ApplyWhenScionExits(AvailableUpdate update) => Applied.Add(update);
}
