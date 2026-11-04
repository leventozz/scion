using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class ChooseAiProviderTests
{
    private static readonly AiProvider First = AiProviders.Sample("first");
    private static readonly AiProvider Second = AiProviders.Sample("second");
    private static readonly AiProvider Third = AiProviders.Sample("third");

    private readonly FakeLastUsedAiProvider _lastUsed = new();

    private static Dictionary<AiProviderId, AiProviderState> States(
        AiProviderState first, AiProviderState second, AiProviderState third) =>
        new()
        {
            [First.Id] = first,
            [Second.Id] = second,
            [Third.Id] = third,
        };

    private Task<AiProvider> ChooseAsync(Dictionary<AiProviderId, AiProviderState> states) =>
        new ChooseAiProvider(new AiProviderCatalog([First, Second, Third]), _lastUsed).ExecuteAsync(states);

    [Fact]
    public async Task Takes_the_provider_used_last_time()
    {
        _lastUsed.Remembered = Third.Id;

        var chosen = await ChooseAsync(States(AiProviderState.Ready, AiProviderState.Ready, AiProviderState.Ready));

        Assert.Equal(Third, chosen);
    }

    [Theory]
    [InlineData(AiProviderState.SignedOut)]
    [InlineData(AiProviderState.NotInstalled)]
    public async Task Takes_the_provider_used_last_time_even_when_it_is_not_ready(AiProviderState state)
    {
        _lastUsed.Remembered = Third.Id;

        var chosen = await ChooseAsync(States(AiProviderState.Ready, AiProviderState.Ready, state));

        Assert.Equal(Third, chosen);
    }

    [Fact]
    public async Task Takes_the_first_ready_provider_when_none_was_used_before()
    {
        var chosen = await ChooseAsync(States(AiProviderState.SignedOut, AiProviderState.Ready, AiProviderState.Ready));

        Assert.Equal(Second, chosen);
    }

    [Fact]
    public async Task Takes_the_first_ready_provider_when_the_one_used_last_is_no_longer_in_the_catalog()
    {
        _lastUsed.Remembered = new AiProviderId("gone");

        var chosen = await ChooseAsync(States(AiProviderState.NotInstalled, AiProviderState.Ready, AiProviderState.Ready));

        Assert.Equal(Second, chosen);
    }

    [Fact]
    public async Task Takes_the_first_provider_of_the_catalog_when_none_is_ready_and_none_was_used()
    {
        var chosen = await ChooseAsync(States(AiProviderState.SignedOut, AiProviderState.NotInstalled, AiProviderState.SignedOut));

        Assert.Equal(First, chosen);
    }

    [Fact]
    public async Task Does_not_change_what_was_remembered()
    {
        _lastUsed.Remembered = Second.Id;

        await ChooseAsync(States(AiProviderState.Ready, AiProviderState.Ready, AiProviderState.Ready));

        Assert.Equal(0, _lastUsed.Writes);
    }
}
