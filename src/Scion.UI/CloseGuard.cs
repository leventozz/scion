namespace Scion.UI;

/// <summary>
/// Stands between the window's close button and an unsaved draft. The host asks <see cref="TryHoldClose"/> when the window
/// is about to close: with a dirty draft the close is held, the user is asked what to do with the draft, and the window
/// is closed through <see cref="IWindowCloser"/> only if they discard it.
/// </summary>
public sealed class CloseGuard(IWindowCloser closer)
{
    private readonly object _gate = new();
    private readonly List<Edit> _edits = [];
    private bool _isAsking;
    private bool _isCloseApproved;

    /// <summary>A page that is editing a file tells the guard how to see whether the draft is dirty and how to ask the user.</summary>
    /// <param name="isDirty">Whether the draft has unsaved changes.</param>
    /// <param name="confirmLeave">Asks the user; true when they chose to discard the draft.</param>
    /// <returns>Dispose it when the page stops editing.</returns>
    public IDisposable Track(Func<bool> isDirty, Func<Task<bool>> confirmLeave)
    {
        var edit = new Edit(isDirty, confirmLeave);
        lock (_gate)
        {
            _edits.Add(edit);
        }

        return new Tracking(() =>
        {
            lock (_gate)
            {
                _edits.Remove(edit);
            }
        });
    }

    /// <returns>True when the close must be held because the user is being asked about a draft.</returns>
    public bool TryHoldClose()
    {
        Edit? dirty;
        lock (_gate)
        {
            if (_isCloseApproved)
            {
                return false;
            }

            dirty = _edits.FirstOrDefault(edit => edit.IsDirty());
            if (dirty is null)
            {
                return false;
            }

            if (_isAsking)
            {
                return true;
            }

            _isAsking = true;
        }

        _ = AskThenCloseAsync(dirty);

        return true;
    }

    private async Task AskThenCloseAsync(Edit edit)
    {
        try
        {
            if (await edit.ConfirmLeaveAsync())
            {
                lock (_gate)
                {
                    _isCloseApproved = true;
                }

                closer.Close();
            }
        }
        catch (Exception)
        {
            // The page that asked is gone; the window stays open and the next close asks again.
        }
        finally
        {
            lock (_gate)
            {
                _isAsking = false;
            }
        }
    }

    private sealed record Edit(Func<bool> IsDirty, Func<Task<bool>> ConfirmLeaveAsync);

    private sealed class Tracking(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}
