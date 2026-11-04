using System.Text.Json;
using System.Text.Json.Serialization;
using Scion.Application.Updating;

namespace Scion.Infrastructure.ProjectRegistry;

/// <summary>
/// Scion's own settings and note about updating, in one file, <c>&lt;scion home&gt;/.scion/updates.json</c>:
/// <c>{ "checkWhenScionStarts": true, "lastCheckedAt": "…", "checkedOnVersion": "1.0.0", "found": { "version": "1.1.0", "releaseNotes": "…" } }</c>.
/// It is the port of both <see cref="IUpdateSettings"/> and <see cref="IUpdateCheckRecord"/>; a write changes only its own fields
/// (read, change, write under one lock, through a temporary file). Like the note of the provider adapted with last, it is a
/// convenience, not data: a file that is missing or cannot be read holds the defaults and no note, and the next write replaces it.
/// </summary>
public sealed class JsonUpdatesFile(string scionHome) : IUpdateSettings, IUpdateCheckRecord
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly UpdateSettings Defaults = new(CheckWhenScionStarts: true);

    private readonly string _file = Path.Combine(scionHome, ".scion", "updates.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<UpdateSettings> ReadAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).CheckWhenScionStarts is { } check ? new UpdateSettings(check) : Defaults;

    async Task<LastUpdateCheck?> IUpdateCheckRecord.ReadAsync(CancellationToken cancellationToken)
    {
        var saved = await LoadAsync(cancellationToken);

        return saved.LastCheckedAt is { } checkedAt && !string.IsNullOrEmpty(saved.CheckedOnVersion)
            ? new LastUpdateCheck(checkedAt, saved.CheckedOnVersion, FoundOf(saved))
            : null;
    }

    public Task WriteAsync(UpdateSettings settings, CancellationToken cancellationToken) =>
        ChangeAsync(saved => saved with { CheckWhenScionStarts = settings.CheckWhenScionStarts }, cancellationToken);

    public Task WriteAsync(LastUpdateCheck check, CancellationToken cancellationToken) =>
        ChangeAsync(
            saved => saved with
            {
                LastCheckedAt = check.CheckedAt,
                CheckedOnVersion = check.CheckedOnVersion,
                Found = check.Found is { } update ? new SavedUpdate(update.Version, update.ReleaseNotes) : null,
            },
            cancellationToken);

    private static AvailableUpdate? FoundOf(Saved saved) =>
        saved.Found is { Version: { Length: > 0 } version } found ? new AvailableUpdate(version, found.ReleaseNotes) : null;

    private async Task ChangeAsync(Func<Saved, Saved> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var temporaryFile = $"{_file}.{Guid.NewGuid():N}.tmp";

            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            await File.WriteAllTextAsync(
                temporaryFile, JsonSerializer.Serialize(change(await LoadAsync(cancellationToken)), JsonOptions), cancellationToken);
            File.Move(temporaryFile, _file, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Saved> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return JsonSerializer.Deserialize<Saved>(await File.ReadAllTextAsync(_file, cancellationToken), JsonOptions) ?? new Saved();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Saved();
        }
    }

    private sealed record Saved
    {
        public bool? CheckWhenScionStarts { get; init; }

        public DateTimeOffset? LastCheckedAt { get; init; }

        public string? CheckedOnVersion { get; init; }

        public SavedUpdate? Found { get; init; }
    }

    private sealed record SavedUpdate(string? Version, string? ReleaseNotes);
}
