using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class AiProviderCatalogTests
{
    private static readonly AiProvider First = AiProviders.Sample("first");
    private static readonly AiProvider Second = AiProviders.Sample("second");

    [Fact]
    public void Lists_the_providers_in_the_order_given()
    {
        var catalog = new AiProviderCatalog([First, Second]);

        Assert.Equal([First, Second], catalog.Providers);
    }

    [Fact]
    public void The_default_is_the_first_provider()
    {
        var catalog = new AiProviderCatalog([First, Second]);

        Assert.Equal(First, catalog.Default);
    }

    [Fact]
    public void Needs_at_least_one_provider() =>
        Assert.Throws<ArgumentException>(() => new AiProviderCatalog([]));

    [Fact]
    public void Rejects_the_same_id_twice() =>
        Assert.Throws<ArgumentException>(() => new AiProviderCatalog([First, AiProviders.Sample("first")]));
}
