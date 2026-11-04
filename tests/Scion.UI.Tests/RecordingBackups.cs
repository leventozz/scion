using Scion.Application.Editing;

namespace Scion.UI.Tests;

internal sealed class RecordingBackups : IFileBackups
{
    private bool _failing;

    public List<EditableFile> Kept { get; } = [];

    public void Fail() => _failing = true;

    public Task KeepPreviousVersionAsync(EditableFile file, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_failing)
        {
            throw new FileBackupFailedException();
        }

        Kept.Add(file);
        return Task.CompletedTask;
    }
}
