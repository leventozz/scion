using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class FakeUpdateFeed : IUpdateFeed
{
    public UpdateCheck Answer { get; set; } = new UpdateCheck.UpToDate("0.1.0");

    public UpdateFailedException? Failure { get; set; }

    public int Asked { get; private set; }

    public Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken)
    {
        Asked++;

        return Failure is null ? Task.FromResult(Answer) : Task.FromException<UpdateCheck>(Failure);
    }
}
