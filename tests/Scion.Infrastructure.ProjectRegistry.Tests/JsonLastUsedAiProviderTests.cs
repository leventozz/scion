using Scion.Application.Adapting;

namespace Scion.Infrastructure.ProjectRegistry.Tests;

public class JsonLastUsedAiProviderTests : IDisposable
{
    private readonly TempDirectory _scionHome = new();

    public void Dispose() => _scionHome.Dispose();

    private string NoteFile => Path.Combine(_scionHome.Path, ".scion", "ai-provider.json");

    private JsonLastUsedAiProvider Note() => new(_scionHome.Path);

    [Fact]
    public async Task Has_no_provider_when_nothing_was_ever_remembered() =>
        Assert.Null(await Note().ReadAsync(CancellationToken.None));

    [Fact]
    public async Task Remembers_the_provider_in_the_scion_folder_of_the_scion_home()
    {
        await Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None);

        Assert.Equal("{\"lastUsed\":\"codex\"}", (await File.ReadAllTextAsync(NoteFile)).ReplaceLineEndings("").Replace(" ", ""));
    }

    [Fact]
    public async Task Gives_back_the_provider_that_was_remembered()
    {
        await Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None);

        Assert.Equal(new AiProviderId("codex"), await Note().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_later_provider_replaces_the_earlier_one()
    {
        await Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None);
        await Note().RememberAsync(new AiProviderId("claude"), CancellationToken.None);

        Assert.Equal(new AiProviderId("claude"), await Note().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Leaves_no_temporary_file_behind()
    {
        await Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None);

        Assert.Equal([NoteFile], Directory.GetFiles(Path.GetDirectoryName(NoteFile)!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("{\"lastUsed\":42}")]
    [InlineData("{\"lastUsed\":\"\"}")]
    [InlineData("{\"lastUsed\":\"   \"}")]
    public async Task A_note_that_cannot_be_read_is_no_provider(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NoteFile)!);
        await File.WriteAllTextAsync(NoteFile, content);

        Assert.Null(await Note().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_next_provider_replaces_a_note_that_cannot_be_read()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NoteFile)!);
        await File.WriteAllTextAsync(NoteFile, "not json");

        await Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None);

        Assert.Equal(new AiProviderId("codex"), await Note().ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_note_that_cannot_be_written_is_an_io_error_for_the_caller_to_decide_about()
    {
        // The scion folder is a file, so nothing can be written under it.
        await File.WriteAllTextAsync(Path.Combine(_scionHome.Path, ".scion"), "");

        await Assert.ThrowsAnyAsync<IOException>(() => Note().RememberAsync(new AiProviderId("codex"), CancellationToken.None));
    }
}
