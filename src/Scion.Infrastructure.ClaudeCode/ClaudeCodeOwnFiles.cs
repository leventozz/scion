using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Claude Code is run with <c>--safe-mode</c>, which keeps its CLAUDE.md, skills, hooks and auto-memory out, so it loads no file
/// of its own beyond what Scion sends.
/// </summary>
public sealed class ClaudeCodeOwnFiles : IAiProviderOwnFiles
{
    public Task<IReadOnlyList<ProviderLoadedFile>> FindAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderLoadedFile>>([]);
}
