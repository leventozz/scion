using Scion.Application.Editing;
using Scion.Application.Skills;

namespace Scion.UI;

/// <summary>
/// The header of a SKILL.md draft as the skill list would show it. A draft is checked once, after a short pause in
/// typing, and the result is kept: rendering the page never checks anything. Leaving or resetting drops a check that
/// has not run yet.
/// </summary>
internal sealed class DraftHeaderCheck(CheckSkillFrontmatter check, TimeProvider time, string folderName, SkillSummary initial)
    : IDisposable
{
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    private CancellationTokenSource? _pending;

    public event Action? Changed;

    public SkillSummary Current { get; private set; } = initial;

    /// <summary>Waits for a pause in typing, then checks this text. A newer text replaces one that is still waiting.</summary>
    public void Schedule(string text)
    {
        Cancel();
        var pending = new CancellationTokenSource();
        _pending = pending;
        _ = CheckAfterPauseAsync(text, pending.Token);
    }

    /// <summary>Takes this summary now (the file was saved or reloaded) and forgets a check that is still waiting.</summary>
    public void Reset(SkillSummary summary)
    {
        Cancel();
        Current = summary;
    }

    public void Dispose() => Cancel();

    private async Task CheckAfterPauseAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Pause, time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var summary = check.Execute(text, folderName);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Current = summary;
        Changed?.Invoke();
    }

    private void Cancel()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }
}
