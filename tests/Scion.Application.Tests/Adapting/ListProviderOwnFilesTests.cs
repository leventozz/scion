using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class ListProviderOwnFilesTests
{
    private sealed class FakeOwnFiles(params ProviderLoadedFile[] files) : IAiProviderOwnFiles
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ProviderLoadedFile>> FindAsync(CancellationToken cancellationToken)
        {
            Reads++;

            return Task.FromResult<IReadOnlyList<ProviderLoadedFile>>(files);
        }
    }

    [Fact]
    public async Task Gives_the_files_the_providers_client_loads_by_itself()
    {
        var file = new ProviderLoadedFile(".codex/AGENTS.md", 1200);
        var ports = new AiProviderPortsStub<IAiProviderOwnFiles>().With("sample", new FakeOwnFiles(file));

        var result = await new ListProviderOwnFiles(ports).ExecuteAsync(new AiProviderId("sample"));

        Assert.Equal([file], result);
    }

    [Fact]
    public async Task Asks_only_the_provider_it_was_given()
    {
        var first = new FakeOwnFiles(new ProviderLoadedFile("a.md", 1));
        var second = new FakeOwnFiles();
        var ports = new AiProviderPortsStub<IAiProviderOwnFiles>().With("first", first).With("second", second);

        var result = await new ListProviderOwnFiles(ports).ExecuteAsync(new AiProviderId("second"));

        Assert.Empty(result);
        Assert.Equal(0, first.Reads);
    }
}
