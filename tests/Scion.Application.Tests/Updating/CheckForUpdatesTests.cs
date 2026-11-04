using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class CheckForUpdatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly AvailableUpdate NewerVersion = new("0.2.0", "Faster start.");

    private readonly FakeUpdateFeed _feed = new();
    private readonly FakeUpdateSettings _settings = new();
    private readonly FakeUpdateCheckRecord _record = new();
    private readonly FakeScionInstallation _installation = new();
    private readonly FixedTimeProvider _time = new(Now);

    private CheckForUpdates Check() => new(_feed, _settings, _record, _installation, _time);

    private void CheckedAgo(TimeSpan ago, AvailableUpdate? found = null, string onVersion = "0.1.0") =>
        _record.Saved = new LastUpdateCheck(Now - ago, onVersion, found);

    [Fact]
    public async Task Tells_the_user_when_a_newer_version_is_available()
    {
        _feed.Answer = new UpdateCheck.Available(NewerVersion);

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateCheck.Available(NewerVersion), result.Value);
    }

    [Fact]
    public async Task Tells_the_user_when_this_is_the_latest_version()
    {
        _feed.Answer = new UpdateCheck.UpToDate("0.2.0");

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateCheck.UpToDate("0.2.0"), result.Value);
    }

    [Theory]
    [InlineData(UpdateCheckTrigger.Startup)]
    [InlineData(UpdateCheckTrigger.Manual)]
    public async Task A_scion_that_is_not_installed_runs_from_source_and_asks_nothing(UpdateCheckTrigger trigger)
    {
        _installation.IsInstalled = false;

        var result = await Check().ExecuteAsync(trigger);

        Assert.Equal(new UpdateCheck.RunningFromSource(), result.Value);
        Assert.Equal(0, _feed.Asked);
    }

    [Fact]
    public async Task A_feed_that_fails_is_an_error_with_its_message()
    {
        _feed.Failure = new UpdateFailedException("No connection.");

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateError("No connection."), result.Error);
    }

    [Fact]
    public async Task A_failed_check_is_not_noted_so_the_next_start_tries_again()
    {
        _feed.Failure = new UpdateFailedException("No connection.");

        await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(0, _record.Writes);
    }

    [Fact]
    public async Task The_start_of_scion_asks_the_feed_when_the_user_wants_that()
    {
        _settings.Saved = new UpdateSettings(CheckWhenScionStarts: true);
        _feed.Answer = new UpdateCheck.Available(NewerVersion);

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.Available(NewerVersion), result.Value);
    }

    [Fact]
    public async Task The_start_of_scion_asks_nothing_when_the_user_switched_the_check_off()
    {
        _settings.Saved = new UpdateSettings(CheckWhenScionStarts: false);

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.Skipped(), result.Value);
        Assert.Equal(0, _feed.Asked);
    }

    [Fact]
    public async Task A_check_the_user_asks_for_ignores_the_switch_for_the_start_of_scion()
    {
        _settings.Saved = new UpdateSettings(CheckWhenScionStarts: false);

        await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(1, _feed.Asked);
    }

    [Fact]
    public async Task A_check_the_user_asks_for_ignores_how_recently_scion_looked()
    {
        CheckedAgo(TimeSpan.FromMinutes(5));

        await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(1, _feed.Asked);
    }

    [Fact]
    public async Task Within_a_day_of_the_last_check_the_start_of_scion_shows_the_update_that_was_found_without_asking()
    {
        CheckedAgo(TimeSpan.FromHours(23), found: NewerVersion);

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.Available(NewerVersion), result.Value);
        Assert.Equal(0, _feed.Asked);
    }

    [Fact]
    public async Task Within_a_day_of_a_check_that_found_nothing_the_start_of_scion_asks_nothing_and_shows_nothing()
    {
        CheckedAgo(TimeSpan.FromHours(1));

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.Skipped(), result.Value);
        Assert.Equal(0, _feed.Asked);
    }

    [Fact]
    public async Task An_update_found_by_another_version_of_scion_is_not_shown_again()
    {
        CheckedAgo(TimeSpan.FromHours(1), found: NewerVersion, onVersion: "0.0.9");

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.Skipped(), result.Value);
        Assert.Equal(0, _feed.Asked);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(100)]
    public async Task A_day_after_the_last_check_the_start_of_scion_asks_again(int hours)
    {
        CheckedAgo(TimeSpan.FromHours(hours), found: NewerVersion);
        _feed.Answer = new UpdateCheck.UpToDate("0.1.0");

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(new UpdateCheck.UpToDate("0.1.0"), result.Value);
        Assert.Equal(1, _feed.Asked);
    }

    [Fact]
    public async Task Without_a_note_of_an_earlier_check_the_start_of_scion_asks()
    {
        await Check().ExecuteAsync(UpdateCheckTrigger.Startup);

        Assert.Equal(1, _feed.Asked);
    }

    [Fact]
    public async Task A_check_that_found_an_update_notes_when_and_on_which_version()
    {
        _installation.Version = "0.1.0";
        _feed.Answer = new UpdateCheck.Available(NewerVersion);

        await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new LastUpdateCheck(Now, "0.1.0", NewerVersion), _record.Saved);
    }

    [Fact]
    public async Task A_check_that_found_nothing_notes_that_too_and_forgets_an_earlier_find()
    {
        CheckedAgo(TimeSpan.FromDays(2), found: NewerVersion);
        _feed.Answer = new UpdateCheck.UpToDate("0.1.0");

        await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new LastUpdateCheck(Now, "0.1.0", null), _record.Saved);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task A_note_that_cannot_be_written_does_not_change_the_answer(Type failure)
    {
        _record.WriteFails = (Exception)Activator.CreateInstance(failure)!;
        _feed.Answer = new UpdateCheck.Available(NewerVersion);

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateCheck.Available(NewerVersion), result.Value);
    }
}
