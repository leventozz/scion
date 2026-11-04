namespace Scion.UI.Tests;

/// <summary>A clock that stands still until a test moves it; timers fire only when <see cref="Advance"/> passes their time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, dueTime);
        _timers.Add(timer);

        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that has come due, once each.</summary>
    public void Advance(TimeSpan span)
    {
        Now += span;

        foreach (var timer in _timers.Where(timer => timer.IsDue(Now)).ToList())
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(FixedTimeProvider clock, TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        private DateTimeOffset? _dueAt = DueAt(clock.Now, dueTime);

        public bool IsDue(DateTimeOffset now) => _dueAt is { } dueAt && dueAt <= now;

        public void Fire()
        {
            _dueAt = null;
            callback(state);
        }

        public bool Change(TimeSpan newDueTime, TimeSpan period)
        {
            _dueAt = DueAt(clock.Now, newDueTime);
            return true;
        }

        public void Dispose() => clock._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private static DateTimeOffset? DueAt(DateTimeOffset now, TimeSpan dueTime) =>
            dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
    }
}
