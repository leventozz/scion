using Scion.Application.Updating;

namespace Scion.Infrastructure.ProjectRegistry.Tests;

public class JsonUpdateSettingsTests : IDisposable
{
    private readonly TempDirectory _scionHome = new();

    public void Dispose() => _scionHome.Dispose();

    private string SettingsFile => Path.Combine(_scionHome.Path, ".scion", "updates.json");

    private JsonUpdateSettings Settings() => new(_scionHome.Path);

    [Fact]
    public async Task Checks_when_scion_starts_until_the_user_says_otherwise() =>
        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: true), await Settings().ReadAsync(CancellationToken.None));

    [Fact]
    public async Task Saves_the_settings_in_the_scion_folder_of_the_scion_home()
    {
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);

        Assert.Equal(
            "{\"checkWhenScionStarts\":false}",
            (await File.ReadAllTextAsync(SettingsFile)).ReplaceLineEndings("").Replace(" ", ""));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Gives_back_the_settings_that_were_saved(bool checkWhenScionStarts)
    {
        await Settings().WriteAsync(new UpdateSettings(checkWhenScionStarts), CancellationToken.None);

        Assert.Equal(new UpdateSettings(checkWhenScionStarts), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Later_settings_replace_the_earlier_ones()
    {
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: true), CancellationToken.None);

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: true), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Leaves_no_temporary_file_behind()
    {
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);

        Assert.Equal([SettingsFile], Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("{\"checkWhenScionStarts\":\"no\"}")]
    [InlineData("{\"checkWhenScionStarts\":null}")]
    public async Task Settings_that_cannot_be_read_are_the_defaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        await File.WriteAllTextAsync(SettingsFile, content);

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: true), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_next_write_replaces_settings_that_cannot_be_read()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        await File.WriteAllTextAsync(SettingsFile, "not json");

        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: false), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Settings_that_cannot_be_written_are_an_io_error_for_the_caller_to_decide_about()
    {
        // The scion folder is a file, so nothing can be written under it.
        await File.WriteAllTextAsync(Path.Combine(_scionHome.Path, ".scion"), "");

        await Assert.ThrowsAnyAsync<IOException>(
            () => Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None));
    }
}
