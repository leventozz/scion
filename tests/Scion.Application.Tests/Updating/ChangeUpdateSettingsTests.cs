using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class ChangeUpdateSettingsTests
{
    private readonly FakeUpdateSettings _settings = new();

    [Fact]
    public async Task Saves_the_new_settings()
    {
        await new ChangeUpdateSettings(_settings).ExecuteAsync(new UpdateSettings(CheckWhenScionStarts: false));

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: false), _settings.Saved);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task A_write_that_fails_does_not_stop_the_user(Type failure)
    {
        _settings.WriteFails = (Exception)Activator.CreateInstance(failure)!;

        var exception = await Record.ExceptionAsync(
            () => new ChangeUpdateSettings(_settings).ExecuteAsync(new UpdateSettings(CheckWhenScionStarts: false)));

        Assert.Null(exception);
    }
}
