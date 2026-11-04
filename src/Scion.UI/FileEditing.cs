using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.UI;

/// <summary>
/// One editing session of one file: the draft, whether it is dirty, what a save found out and the questions the user
/// has to answer (changed on disk, discard changes). It holds no markup; the page renders it and listens to
/// <see cref="Changed"/>.
/// </summary>
internal sealed class FileEditing(OpenFileForEditing open, SaveEditedFile save, EditableFile file)
{
    private EditBaseline? _baseline;
    private TaskCompletionSource<bool>? _leaving;

    public event Action? Changed;

    public EditableFile File => file;

    public string Original { get; private set; } = "";

    public string Draft { get; private set; } = "";

    public bool IsDirty => Draft != Original;

    public EditMode Mode { get; private set; } = EditMode.Edit;

    /// <summary>Why the last open or save failed, as the user reads it; shown next to the buttons.</summary>
    public string? Error { get; private set; }

    /// <summary>A save that worked but has something to say.</summary>
    public string? Note { get; private set; }

    public bool IsSaving { get; private set; }

    /// <summary>Set while the user has to decide what to do with a file that changed under them.</summary>
    public DiskConflict? Conflict { get; private set; }

    public bool IsConfirmingDiscard { get; private set; }

    public async Task<bool> StartAsync()
    {
        var result = await open.ExecuteAsync(file);
        if (!result.IsSuccess)
        {
            Error = EditMessages.For(result.Error);
            Raise();
            return false;
        }

        Apply(result.Value);
        Raise();
        return true;
    }

    public void SetDraft(string text)
    {
        Draft = text;
        Raise();
    }

    public void ShowEditor() => SetMode(EditMode.Edit);

    public void ShowPreview() => SetMode(EditMode.Preview);

    public Task<SaveOutcome> SaveAsync() => RunSaveAsync(SaveMode.CheckForChanges);

    public Task<SaveOutcome> OverwriteAsync() => RunSaveAsync(SaveMode.Overwrite);

    /// <summary>Takes the file as it is on disk now; the draft is dropped.</summary>
    public async Task ReloadAsync()
    {
        Conflict = null;
        var result = await open.ExecuteAsync(file);

        if (result.IsSuccess)
        {
            Apply(result.Value);
        }
        else
        {
            Error = EditMessages.For(result.Error);
        }

        Raise();
    }

    public void CancelConflict()
    {
        Conflict = null;
        Raise();
    }

    /// <summary>
    /// True when the user may leave: nothing is unsaved, or they chose to discard. A dirty draft asks first; asking
    /// again while the question is open gives the same answer.
    /// </summary>
    public Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty)
        {
            return Task.FromResult(true);
        }

        if (_leaving is null)
        {
            _leaving = new TaskCompletionSource<bool>();
            IsConfirmingDiscard = true;
            Raise();
        }

        return _leaving.Task;
    }

    public void KeepEditing() => AnswerLeaving(leave: false);

    public void Discard() => AnswerLeaving(leave: true);

    private async Task<SaveOutcome> RunSaveAsync(SaveMode mode)
    {
        var text = Draft;
        Conflict = null;
        Error = null;
        Note = null;
        IsSaving = true;
        Raise();

        Result<SavedFile, SaveFileError> result;
        try
        {
            result = await save.ExecuteAsync(file, text, _baseline!, mode);
        }
        finally
        {
            IsSaving = false;
        }

        var outcome = Apply(result, text);
        Raise();
        return outcome;
    }

    private SaveOutcome Apply(Result<SavedFile, SaveFileError> result, string savedText)
    {
        if (result.IsSuccess)
        {
            Original = savedText;
            _baseline = result.Value.NewBaseline;
            Note = result.Value.BackupSkipped ? EditMessages.BackupSkipped : null;
            return SaveOutcome.Saved;
        }

        switch (result.Error)
        {
            case SaveFileError.ChangedOnDisk:
                Conflict = DiskConflict.Changed;
                return SaveOutcome.Conflict;
            case SaveFileError.DeletedOnDisk:
                Conflict = DiskConflict.Deleted;
                return SaveOutcome.Conflict;
            default:
                Error = EditMessages.For(result.Error);
                return SaveOutcome.Failed;
        }
    }

    private void Apply(EditableText text)
    {
        Original = text.Text;
        Draft = text.Text;
        _baseline = text.Baseline;
        Error = null;
        Note = null;
    }

    private void SetMode(EditMode mode)
    {
        Mode = mode;
        Raise();
    }

    private void AnswerLeaving(bool leave)
    {
        if (_leaving is not { } leaving)
        {
            return;
        }

        _leaving = null;
        IsConfirmingDiscard = false;
        Raise();
        leaving.SetResult(leave);
    }

    private void Raise() => Changed?.Invoke();
}
