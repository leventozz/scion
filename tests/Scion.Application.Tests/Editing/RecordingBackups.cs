using Scion.Application.Editing;

namespace Scion.Application.Tests.Editing;

internal sealed class RecordingBackups(List<string>? log = null) : IFileBackups
{
    private bool _failing;

    public List<(EditableFile File, DateTimeOffset Now)> Kept { get; } = [];

    public void Fail() => _failing = true;

    public Task KeepPreviousVersionAsync(EditableFile file, DateTimeOffset now, CancellationToken cancellationToken)
    {
        log?.Add("backup");

        if (_failing)
        {
            throw new FileBackupFailedException();
        }

        Kept.Add((file, now));
        return Task.CompletedTask;
    }
}
