using Microsoft.AspNetCore.Components;

namespace Scion.UI;

/// <summary>
/// A component that shows the update session and follows it. The session changes on whatever thread did the work (a download
/// reports from its own, often many times a second), so the changes are collected: a burst costs one render.
/// </summary>
public abstract class UpdateSessionComponent : ComponentBase, IDisposable
{
    private int _renderPending;
    private bool _disposed;

    [Inject]
    protected UpdateSession Session { get; set; } = default!;

    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    public void Dispose()
    {
        _disposed = true;
        Session.Changed -= OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        if (Interlocked.Exchange(ref _renderPending, 1) == 1)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _renderPending, 0);
            if (!_disposed)
            {
                StateHasChanged();
            }
        });
    }
}
