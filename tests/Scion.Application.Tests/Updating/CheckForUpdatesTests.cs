using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class CheckForUpdatesTests
{
    private static readonly AvailableUpdate NewerVersion = new("0.2.0", "Faster start.");

    private readonly FakeUpdateFeed _feed = new();
    private readonly FakeUpdateSettings _settings = new();

    private CheckForUpdates Check() => new(_feed, _settings);

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

    [Fact]
    public async Task Tells_the_user_when_scion_runs_from_source()
    {
        _feed.Answer = new UpdateCheck.RunningFromSource();

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateCheck.RunningFromSource(), result.Value);
    }

    [Fact]
    public async Task A_feed_that_fails_is_an_error_with_its_message()
    {
        _feed.Failure = new UpdateFailedException("No connection.");

        var result = await Check().ExecuteAsync(UpdateCheckTrigger.Manual);

        Assert.Equal(new UpdateError("No connection."), result.Error);
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
}
