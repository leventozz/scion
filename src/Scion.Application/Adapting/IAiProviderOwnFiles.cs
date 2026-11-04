namespace Scion.Application.Adapting;

/// <summary>A file the provider's own client loads by itself, which Scion neither reads nor sends.</summary>
/// <param name="HomeRelativePath">Where it is, relative to the home folder and with forward slashes (".codex/AGENTS.md").</param>
/// <param name="Bytes">Its size.</param>
public sealed record ProviderLoadedFile(string HomeRelativePath, long Bytes);

/// <summary>
/// The files the provider's client reads on its own, whatever Scion sends (its personal instruction file, say). Only their
/// existence and size are looked at: the content is never read, never part of the prompt and never counted against a limit.
/// The dialog lists them so the user knows what else goes with the request.
/// </summary>
public interface IAiProviderOwnFiles
{
    Task<IReadOnlyList<ProviderLoadedFile>> FindAsync(CancellationToken cancellationToken);
}
