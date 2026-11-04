namespace Scion.UI.Tests;

internal sealed class RecordingWindowCloser : IWindowCloser
{
    public int Closed { get; private set; }

    public void Close() => Closed++;
}
