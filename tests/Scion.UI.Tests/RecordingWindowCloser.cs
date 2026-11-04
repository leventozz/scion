namespace Scion.UI.Tests;

internal sealed class RecordingWindowCloser : IWindowCloser
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _closeCount;

    public int Closed => Volatile.Read(ref _closeCount);

    /// <summary>
    /// Completes when the window was closed. Closing renders nothing, so a test that waits for it must not wait for a render
    /// (<c>WaitForAssertion</c> looks again only after a render): it waits on this.
    /// </summary>
    public Task WhenClosed => _closed.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public void Close()
    {
        Interlocked.Increment(ref _closeCount);
        _closed.TrySetResult();
    }
}
