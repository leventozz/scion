using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class AiProviderTests
{
    [Fact]
    public void Knows_the_account_the_user_signs_in_with_apart_from_its_own_name()
    {
        var provider = new AiProvider(
            new AiProviderId("sample"), "Sample", "Sample CLI", "Some account", new Uri("https://example.com"), AiUsageNotes.Always("Note."));

        Assert.Equal("Some account", provider.SignInWith);
        Assert.Equal("Sample", provider.Name);
    }
}
