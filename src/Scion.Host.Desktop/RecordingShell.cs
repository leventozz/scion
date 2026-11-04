using Scion.UI;

namespace Scion.Host.Desktop;

/// <summary>
/// Test seam for UI smoke runs (SCION_TEST_SHELL_LOG): instead of opening Explorer or a browser, the shell actions
/// are appended to a file as <c>reveal &lt;path&gt;</c> and <c>open &lt;url&gt;</c>.
/// </summary>
internal sealed class RecordingShell(string logFile) : IFolderRevealer, IExternalLinks
{
    private readonly object _gate = new();

    public Task ShowAsync(string folderPath) => AppendAsync($"reveal {folderPath}");

    public Task OpenAsync(Uri uri) => AppendAsync($"open {uri.AbsoluteUri}");

    private Task AppendAsync(string line)
    {
        lock (_gate)
        {
            File.AppendAllLines(logFile, [line]);
        }

        return Task.CompletedTask;
    }
}
