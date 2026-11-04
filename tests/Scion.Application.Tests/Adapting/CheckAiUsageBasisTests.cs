using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class CheckAiUsageBasisTests
{
    [Theory]
    [InlineData(AiUsageBasis.Subscription)]
    [InlineData(AiUsageBasis.ApiKey)]
    [InlineData(AiUsageBasis.Unknown)]
    public async Task Gives_what_the_reader_reports(AiUsageBasis basis)
    {
        var ports = new AiProviderPortsStub<IAiUsageBasisReader>().With("sample", new Reader(basis));

        Assert.Equal(basis, await new CheckAiUsageBasis(ports).ExecuteAsync(new AiProviderId("sample")));
    }

    [Fact]
    public async Task Asks_only_the_reader_of_the_provider_it_was_given()
    {
        var ports = new AiProviderPortsStub<IAiUsageBasisReader>()
            .With("first", new Reader(AiUsageBasis.Subscription))
            .With("second", new Reader(AiUsageBasis.ApiKey));

        Assert.Equal(AiUsageBasis.ApiKey, await new CheckAiUsageBasis(ports).ExecuteAsync(new AiProviderId("second")));
    }

    private sealed class Reader(AiUsageBasis basis) : IAiUsageBasisReader
    {
        public Task<AiUsageBasis> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(basis);
    }
}
