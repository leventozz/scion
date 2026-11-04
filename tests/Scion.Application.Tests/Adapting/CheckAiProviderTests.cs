using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class CheckAiProviderTests
{
    private static readonly AiProviderId Sample = new("sample");

    [Theory]
    [InlineData(AiProviderState.NotInstalled)]
    [InlineData(AiProviderState.SignedOut)]
    [InlineData(AiProviderState.Ready)]
    public async Task Gives_the_state_of_the_provider(AiProviderState state)
    {
        var status = new FakeAiProviderStatus { State = state };
        var ports = new AiProviderPortsStub<IAiProviderStatus>().With("sample", status);

        var result = await new CheckAiProvider(ports).ExecuteAsync(Sample);

        Assert.Equal(state, result);
    }

    [Fact]
    public async Task Asks_only_the_provider_it_was_given()
    {
        var first = new FakeAiProviderStatus { State = AiProviderState.Ready };
        var second = new FakeAiProviderStatus { State = AiProviderState.NotInstalled };
        var ports = new AiProviderPortsStub<IAiProviderStatus>().With("first", first).With("second", second);

        var result = await new CheckAiProvider(ports).ExecuteAsync(new AiProviderId("second"));

        Assert.Equal(AiProviderState.NotInstalled, result);
        Assert.Equal(0, first.StateChecks);
    }
}
