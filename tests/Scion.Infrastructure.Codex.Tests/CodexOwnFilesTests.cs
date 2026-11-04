using Scion.Application.Adapting;

namespace Scion.Infrastructure.Codex.Tests;

/// <summary>
/// Codex reads its personal instruction file by itself: <c>AGENTS.override.md</c> when that has content, else
/// <c>AGENTS.md</c> (it takes only the first file that is not empty). Scion only reports which one and how large.
/// </summary>
public class CodexOwnFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "scion-tests", Guid.NewGuid().ToString("N"));

    public CodexOwnFilesTests() => Directory.CreateDirectory(Path.Combine(_home, ".codex"));

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_home, ".codex", name), content);

    private Task<IReadOnlyList<ProviderLoadedFile>> FindAsync() => new CodexOwnFiles(_home).FindAsync(CancellationToken.None);

    [Fact]
    public async Task Lists_the_personal_instruction_file_with_its_size()
    {
        Write("AGENTS.md", new string('a', 1500));

        var files = await FindAsync();

        Assert.Equal([new ProviderLoadedFile(".codex/AGENTS.md", 1500)], files);
    }

    [Fact]
    public async Task The_override_file_wins_when_it_has_content()
    {
        Write("AGENTS.md", "Personal rules.");
        Write("AGENTS.override.md", "Override rules.");

        var files = await FindAsync();

        Assert.Equal([new ProviderLoadedFile(".codex/AGENTS.override.md", 15)], files);
    }

    [Fact]
    public async Task An_empty_override_file_is_skipped_as_codex_skips_it()
    {
        Write("AGENTS.md", "Personal rules.");
        Write("AGENTS.override.md", "");

        var files = await FindAsync();

        Assert.Equal([new ProviderLoadedFile(".codex/AGENTS.md", 15)], files);
    }

    [Fact]
    public async Task An_empty_instruction_file_is_not_listed()
    {
        Write("AGENTS.md", "");

        Assert.Empty(await FindAsync());
    }

    [Fact]
    public async Task Lists_nothing_when_there_is_no_file()
    {
        Assert.Empty(await FindAsync());
    }

    [Fact]
    public async Task Lists_nothing_when_there_is_no_codex_folder()
    {
        Directory.Delete(Path.Combine(_home, ".codex"));

        Assert.Empty(await FindAsync());
    }

    [Fact]
    public async Task A_file_that_is_a_link_is_not_looked_at()
    {
        var target = Path.Combine(_home, "elsewhere.md");
        File.WriteAllText(target, "Shared rules.");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_home, ".codex", "AGENTS.md"), target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Windows without the right to create links: there is nothing to check here.
            return;
        }

        Assert.Empty(await FindAsync());
    }
}
