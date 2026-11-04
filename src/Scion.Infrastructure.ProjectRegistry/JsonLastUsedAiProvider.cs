using System.Text.Json;
using Scion.Application.Adapting;

namespace Scion.Infrastructure.ProjectRegistry;

/// <summary>
/// Scion's own note of the provider adapted with last: <c>{ "lastUsed": "codex" }</c> in <c>&lt;scion home&gt;/.scion/ai-provider.json</c>.
/// It is a convenience, not data: a note that is missing or cannot be read is no provider, and the next write replaces it (the
/// rule that keeps an unreadable project registry untouched does not apply to a file this small and this easy to lose).
/// </summary>
public sealed class JsonLastUsedAiProvider(string scionHome) : ILastUsedAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _noteFile = Path.Combine(scionHome, ".scion", "ai-provider.json");

    public async Task<AiProviderId?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var note = JsonSerializer.Deserialize<Note>(await File.ReadAllTextAsync(_noteFile, cancellationToken), JsonOptions);

            return string.IsNullOrWhiteSpace(note?.LastUsed) ? null : new AiProviderId(note.LastUsed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task RememberAsync(AiProviderId provider, CancellationToken cancellationToken)
    {
        var temporaryFile = $"{_noteFile}.{Guid.NewGuid():N}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(_noteFile)!);
        await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(new Note { LastUsed = provider.Value }, JsonOptions), cancellationToken);
        File.Move(temporaryFile, _noteFile, overwrite: true);
    }

    private sealed class Note
    {
        public string? LastUsed { get; init; }
    }
}
