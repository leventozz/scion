using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class RecordingUpdateApplier : IUpdateApplier
{
    public List<AvailableUpdate> Applied { get; } = [];

    public UpdateFailedException? Failure { get; set; }

    public void ApplyWhenScionExits(AvailableUpdate update)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Applied.Add(update);
    }
}
