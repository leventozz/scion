using Scion.Application.Projects;

namespace Scion.Application.Tests.Projects;

public class StartFreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProjectRegistryRecovery _recovery = new();
    private readonly StartFresh _startFresh;

    public StartFreshTests() => _startFresh = new StartFresh(_recovery, new FixedTimeProvider(Now));

    [Fact]
    public async Task Sets_the_registry_aside_at_the_current_time_and_returns_where_the_backup_is()
    {
        var result = await _startFresh.ExecuteAsync();

        Assert.Equal(InMemoryProjectRegistryRecovery.BackupPath, result.Value);
        Assert.Equal(Now, _recovery.SetAsideAt);
    }

    [Fact]
    public async Task Fails_with_write_failed_when_the_registry_cannot_be_set_aside()
    {
        _recovery.FailsToWrite = true;

        var result = await _startFresh.ExecuteAsync();

        Assert.Equal(new RegistryFailure(RegistryFailureKind.WriteFailed, InMemoryProjectRegistry.RegistryFile), result.Error);
    }
}
