using Scion.Application.Updating;

namespace Scion.Application.Tests.Updating;

public class ReadUpdateSettingsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Gives_the_settings_the_user_saved(bool checkWhenScionStarts)
    {
        var settings = new FakeUpdateSettings { Saved = new UpdateSettings(checkWhenScionStarts) };

        Assert.Equal(new UpdateSettings(checkWhenScionStarts), await new ReadUpdateSettings(settings).ExecuteAsync());
    }
}
