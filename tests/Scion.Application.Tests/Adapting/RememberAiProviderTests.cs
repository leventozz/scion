using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class RememberAiProviderTests
{
    private readonly FakeLastUsedAiProvider _lastUsed = new();

    [Fact]
    public async Task Remembers_the_provider_that_was_used()
    {
        await new RememberAiProvider(_lastUsed).ExecuteAsync(new AiProviderId("codex"));

        Assert.Equal(new AiProviderId("codex"), _lastUsed.Remembered);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task A_write_that_fails_does_not_stop_the_adaptation(Type failure)
    {
        _lastUsed.WriteFails = (Exception)Activator.CreateInstance(failure)!;

        var exception = await Record.ExceptionAsync(() => new RememberAiProvider(_lastUsed).ExecuteAsync(new AiProviderId("codex")));

        Assert.Null(exception);
    }
}
