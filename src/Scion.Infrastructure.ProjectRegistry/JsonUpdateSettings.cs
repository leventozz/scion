using System.Text.Json;
using Scion.Application.Updating;

namespace Scion.Infrastructure.ProjectRegistry;

/// <summary>
/// Scion's own settings about updating: <c>{ "checkWhenScionStarts": true }</c> in <c>&lt;scion home&gt;/.scion/updates.json</c>.
/// Like the note of the provider adapted with last, they are a convenience, not data: settings that are missing or cannot be read
/// are the defaults, and the next write replaces them.
/// </summary>
public sealed class JsonUpdateSettings(string scionHome) : IUpdateSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly UpdateSettings Defaults = new(CheckWhenScionStarts: true);

    private readonly string _settingsFile = Path.Combine(scionHome, ".scion", "updates.json");

    public async Task<UpdateSettings> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(await File.ReadAllTextAsync(_settingsFile, cancellationToken), JsonOptions);

            return saved?.CheckWhenScionStarts is { } check ? new UpdateSettings(check) : Defaults;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Defaults;
        }
    }

    public async Task WriteAsync(UpdateSettings settings, CancellationToken cancellationToken)
    {
        var temporaryFile = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
        await File.WriteAllTextAsync(
            temporaryFile, JsonSerializer.Serialize(new Saved { CheckWhenScionStarts = settings.CheckWhenScionStarts }, JsonOptions), cancellationToken);
        File.Move(temporaryFile, _settingsFile, overwrite: true);
    }

    private sealed class Saved
    {
        public bool? CheckWhenScionStarts { get; init; }
    }
}
