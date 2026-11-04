using Scion.Application.Updating;

namespace Scion.Infrastructure.ProjectRegistry.Tests;

public class JsonUpdatesFileTests : IDisposable
{
    private readonly TempDirectory _scionHome = new();

    public void Dispose() => _scionHome.Dispose();

    private string SettingsFile => Path.Combine(_scionHome.Path, ".scion", "updates.json");

    private JsonUpdatesFile Settings() => new(_scionHome.Path);

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

    private static readonly LastUpdateCheck FoundUpdate =
        new(new DateTimeOffset(2026, 11, 4, 12, 30, 0, TimeSpan.Zero), "1.0.0", new AvailableUpdate("1.1.0", "Faster start."));

    /// <summary>The file is both ports; its note of the last check is the one a caller reaches through <see cref="IUpdateCheckRecord"/>.</summary>
    private Task<LastUpdateCheck?> ReadCheckAsync(JsonUpdatesFile? file = null) =>
        ((IUpdateCheckRecord)(file ?? Settings())).ReadAsync(CancellationToken.None);

    private async Task WriteFileAsync(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        await File.WriteAllTextAsync(SettingsFile, content);
    }

    [Fact]
    public async Task Has_no_note_of_a_check_until_one_was_made() =>
        Assert.Null(await ReadCheckAsync());

    [Fact]
    public async Task Gives_back_the_check_that_was_noted()
    {
        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);

        Assert.Equal(FoundUpdate, await ReadCheckAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Faster start.")]
    public async Task Gives_back_a_found_update_with_or_without_notes(string? notes)
    {
        var check = FoundUpdate with { Found = new AvailableUpdate("1.1.0", notes) };
        await Settings().WriteAsync(check, CancellationToken.None);

        Assert.Equal(check, await ReadCheckAsync());
    }

    [Fact]
    public async Task Gives_back_a_check_that_found_nothing()
    {
        var check = FoundUpdate with { Found = null };
        await Settings().WriteAsync(check, CancellationToken.None);

        Assert.Equal(check, await ReadCheckAsync());
    }

    [Fact]
    public async Task A_later_check_replaces_what_an_earlier_one_found()
    {
        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);
        await Settings().WriteAsync(FoundUpdate with { Found = null }, CancellationToken.None);

        Assert.Null((await ReadCheckAsync())!.Found);
    }

    [Fact]
    public async Task Notes_the_check_beside_the_settings_in_one_file()
    {
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);
        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);

        Assert.Equal(
            "{\"checkWhenScionStarts\":false,\"lastCheckedAt\":\"2026-11-04T12:30:00+00:00\",\"checkedOnVersion\":\"1.0.0\","
            + "\"found\":{\"version\":\"1.1.0\",\"releaseNotes\":\"Faster start.\"}}",
            (await File.ReadAllTextAsync(SettingsFile)).ReplaceLineEndings("").Replace(" ", "").Replace("Fasterstart.", "Faster start."));
    }

    [Fact]
    public async Task Writing_the_settings_keeps_the_note_of_the_check()
    {
        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);

        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);

        Assert.Equal(FoundUpdate, await ReadCheckAsync());
    }

    [Fact]
    public async Task Writing_the_note_of_a_check_keeps_the_settings()
    {
        await Settings().WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None);

        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: false), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Two_writes_at_once_both_survive()
    {
        var file = Settings();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => index % 2 == 0
            ? file.WriteAsync(new UpdateSettings(CheckWhenScionStarts: false), CancellationToken.None)
            : file.WriteAsync(FoundUpdate, CancellationToken.None)));

        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: false), await file.ReadAsync(CancellationToken.None));
        Assert.Equal(FoundUpdate, await ReadCheckAsync(file));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"checkWhenScionStarts\":false}")]
    [InlineData("{\"lastCheckedAt\":\"yesterday\",\"checkedOnVersion\":\"1.0.0\"}")]
    [InlineData("{\"lastCheckedAt\":\"2026-11-04T12:30:00+00:00\"}")]
    [InlineData("{\"lastCheckedAt\":\"2026-11-04T12:30:00+00:00\",\"checkedOnVersion\":\"\"}")]
    public async Task A_note_that_cannot_be_read_is_no_note(string content)
    {
        await WriteFileAsync(content);

        Assert.Null(await ReadCheckAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":\"\"}")]
    public async Task A_found_update_that_cannot_be_read_is_no_find_but_the_check_still_counts(string found)
    {
        await WriteFileAsync($"{{\"lastCheckedAt\":\"2026-11-04T12:30:00+00:00\",\"checkedOnVersion\":\"1.0.0\",\"found\":{found}}}");

        Assert.Equal(FoundUpdate with { Found = null }, await ReadCheckAsync());
    }

    [Fact]
    public async Task The_next_write_replaces_a_file_that_cannot_be_read()
    {
        await WriteFileAsync("not json");

        await Settings().WriteAsync(FoundUpdate, CancellationToken.None);

        Assert.Equal(FoundUpdate, await ReadCheckAsync());
        Assert.Equal(new UpdateSettings(CheckWhenScionStarts: true), await Settings().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_note_that_cannot_be_written_is_an_io_error_for_the_caller_to_decide_about()
    {
        await File.WriteAllTextAsync(Path.Combine(_scionHome.Path, ".scion"), "");

        await Assert.ThrowsAnyAsync<IOException>(() => Settings().WriteAsync(FoundUpdate, CancellationToken.None));
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
