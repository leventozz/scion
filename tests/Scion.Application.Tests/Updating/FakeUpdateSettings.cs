using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

internal sealed class FakeUpdateSettings : IUpdateSettings
{
    public UpdateSettings Saved { get; set; } = new(CheckWhenScionStarts: true);

    public Exception? WriteFails { get; set; }

    public Task<UpdateSettings> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

    public Task WriteAsync(UpdateSettings settings, CancellationToken cancellationToken)
    {
        if (WriteFails is not null)
        {
            throw WriteFails;
        }

        Saved = settings;

        return Task.CompletedTask;
    }
}
