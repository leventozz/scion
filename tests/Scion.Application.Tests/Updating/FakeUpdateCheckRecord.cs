using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class FakeUpdateCheckRecord : IUpdateCheckRecord
{
    public LastUpdateCheck? Saved { get; set; }

    public Exception? WriteFails { get; set; }

    public int Writes { get; private set; }

    public Task<LastUpdateCheck?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

    public Task WriteAsync(LastUpdateCheck check, CancellationToken cancellationToken)
    {
        Writes++;
        if (WriteFails is not null)
        {
            throw WriteFails;
        }

        Saved = check;

        return Task.CompletedTask;
    }
}
