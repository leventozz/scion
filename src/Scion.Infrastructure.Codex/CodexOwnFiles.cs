using Scion.Application.Adapting;

namespace Scion.Infrastructure.Codex;

/// <summary>
/// Codex loads its personal instruction file by itself, however it is started: <c>~/.codex/AGENTS.override.md</c> when that
/// has content, else <c>~/.codex/AGENTS.md</c> (only the first file that is not empty counts). Only the name and the size are
/// looked at, never the content. A file that is a link is shared with another place and is left alone.
/// <c>CODEX_HOME</c> is out of scope: the personal folder is the one under the home directory.
/// </summary>
public sealed class CodexOwnFiles(string homeDirectory) : IAiProviderOwnFiles
{
    private static readonly string[] Names = ["AGENTS.override.md", "AGENTS.md"];

    public Task<IReadOnlyList<ProviderLoadedFile>> FindAsync(CancellationToken cancellationToken)
    {
        var file = Names
            .Select(name => new FileInfo(Path.Combine(homeDirectory, ".codex", name)))
            .FirstOrDefault(candidate => candidate.Exists && candidate.LinkTarget is null && candidate.Length > 0);

        IReadOnlyList<ProviderLoadedFile> files = file is null ? [] : [new ProviderLoadedFile($".codex/{file.Name}", file.Length)];

        return Task.FromResult(files);
    }
}
