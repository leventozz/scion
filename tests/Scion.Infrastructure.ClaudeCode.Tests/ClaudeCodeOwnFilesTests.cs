namespace Scion.Infrastructure.ClaudeCode.Tests;

public class ClaudeCodeOwnFilesTests
{
    [Fact]
    public async Task Loads_nothing_by_itself_because_safe_mode_keeps_the_personal_instruction_file_out() =>
        Assert.Empty(await new ClaudeCodeOwnFiles().FindAsync(CancellationToken.None));
}
