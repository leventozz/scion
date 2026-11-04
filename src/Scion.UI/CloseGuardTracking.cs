namespace Scion.UI;

internal static class CloseGuardTracking
{
    /// <summary>
    /// Registers an editing session with the guard. The guard is asked from the window's thread, so the question to the
    /// user is put to the editing session on the renderer's thread, where its state belongs.
    /// </summary>
    /// <param name="runOnRenderer">The page's <c>InvokeAsync</c>.</param>
    public static IDisposable Track(CloseGuard guard, FileEditing editing, Func<Func<Task>, Task> runOnRenderer) =>
        guard.Track(() => editing.IsDirty, () => ConfirmLeaveAsync(editing, runOnRenderer));

    private static Task<bool> ConfirmLeaveAsync(FileEditing editing, Func<Func<Task>, Task> runOnRenderer)
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = runOnRenderer(async () => answer.SetResult(await editing.ConfirmLeaveAsync()));

        return answer.Task;
    }
}
