namespace Scion.UI.Tests;

public class CloseGuardTests
{
    private readonly RecordingWindowCloser _closer = new();
    private readonly CloseGuard _guard;

    public CloseGuardTests() => _guard = new CloseGuard(_closer);

    [Fact]
    public void Lets_the_window_close_when_nothing_is_being_edited()
    {
        Assert.False(_guard.TryHoldClose());
        Assert.Equal(0, _closer.Closed);
    }

    [Fact]
    public void Lets_the_window_close_when_the_draft_is_clean()
    {
        var edit = new Edit { IsDirty = false };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        Assert.False(_guard.TryHoldClose());
        Assert.Equal(0, edit.Asked);
    }

    [Fact]
    public void Holds_the_close_and_asks_when_the_draft_is_dirty()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        Assert.True(_guard.TryHoldClose());

        Assert.Equal(1, edit.Asked);
        Assert.Equal(0, _closer.Closed);
    }

    [Fact]
    public void Closes_the_window_once_the_user_chose_to_discard_and_does_not_hold_that_close()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);
        _guard.TryHoldClose();

        edit.Answer(leave: true);

        Assert.Equal(1, _closer.Closed);
        Assert.False(_guard.TryHoldClose());
    }

    [Fact]
    public void Keeps_the_window_open_when_the_user_chose_to_keep_editing_and_asks_again_next_time()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);
        _guard.TryHoldClose();

        edit.Answer(leave: false);

        Assert.Equal(0, _closer.Closed);
        Assert.True(_guard.TryHoldClose());
        Assert.Equal(2, edit.Asked);
    }

    [Fact]
    public void Asks_only_once_while_the_question_is_open()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        Assert.True(_guard.TryHoldClose());
        Assert.True(_guard.TryHoldClose());

        Assert.Equal(1, edit.Asked);
    }

    [Fact]
    public void Forgets_an_edit_that_is_no_longer_tracked()
    {
        var edit = new Edit { IsDirty = true };
        var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        tracking.Dispose();

        Assert.False(_guard.TryHoldClose());
    }

    [Fact]
    public void A_failing_question_leaves_the_window_open_and_can_be_asked_again()
    {
        var calls = 0;
        using var tracking = _guard.Track(() => true, () =>
        {
            calls++;
            return Task.FromException<bool>(new InvalidOperationException("the page is gone"));
        });

        Assert.True(_guard.TryHoldClose());
        Assert.True(_guard.TryHoldClose());

        Assert.Equal(2, calls);
        Assert.Equal(0, _closer.Closed);
    }

    [Fact]
    public async Task May_leave_when_nothing_is_being_edited()
    {
        Assert.True(await _guard.ConfirmLeaveAsync());
    }

    [Fact]
    public async Task May_leave_when_the_draft_is_clean_without_asking()
    {
        var edit = new Edit { IsDirty = false };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        Assert.True(await _guard.ConfirmLeaveAsync());
        Assert.Equal(0, edit.Asked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Leaving_with_a_dirty_draft_is_what_the_user_answers(bool discard)
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        var leaving = _guard.ConfirmLeaveAsync();
        edit.Answer(discard);

        Assert.Equal(discard, await leaving);
        Assert.Equal(1, edit.Asked);
    }

    [Fact]
    public async Task Does_not_stack_a_second_question_on_one_that_is_open()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);
        _guard.TryHoldClose();

        Assert.False(await _guard.ConfirmLeaveAsync());
        Assert.Equal(1, edit.Asked);
    }

    [Fact]
    public void Closing_now_closes_the_window_and_that_close_is_not_held_even_with_a_dirty_draft()
    {
        var edit = new Edit { IsDirty = true };
        using var tracking = _guard.Track(edit.IsDirtyNow, edit.ConfirmLeaveAsync);

        _guard.CloseNow();

        Assert.Equal(1, _closer.Closed);
        Assert.False(_guard.TryHoldClose());
        Assert.Equal(0, edit.Asked);
    }

    private sealed class Edit
    {
        private readonly TaskCompletionSource<bool> _answer = new();

        public bool IsDirty { get; set; }

        public int Asked { get; private set; }

        public bool IsDirtyNow() => IsDirty;

        public Task<bool> ConfirmLeaveAsync()
        {
            Asked++;
            return _answer.Task;
        }

        public void Answer(bool leave) => _answer.SetResult(leave);
    }
}
