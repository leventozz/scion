using System.Text.Json;
using Scion.Application.Projects;
using Scion.Domain;

namespace Scion.Infrastructure.ProjectRegistry;

public sealed class JsonProjectRegistry(string scionHome, StringComparison pathComparison)
    : IProjectRegistry, IProjectRegistryRecovery
{
    private static readonly char[] Separators = ['/', '\\'];

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _registryFile = Path.Combine(scionHome, ".scion", "projects.json");

    public Task<IReadOnlyList<RegisteredProject>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RegisteredProject>>(Load());

    public Task<RegisteredProject?> FindByPathAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(Load().FirstOrDefault(project => SamePath(project.Path, path)));

    public Task SaveAsync(RegisteredProject project, CancellationToken cancellationToken)
    {
        var projects = Load();
        projects.RemoveAll(existing => SamePath(existing.Path, project.Path));
        projects.Add(project with { Path = Normalize(project.Path) });

        Write(projects);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string path, CancellationToken cancellationToken)
    {
        var projects = Load();

        if (projects.RemoveAll(existing => SamePath(existing.Path, path)) > 0)
        {
            Write(projects);
        }

        return Task.CompletedTask;
    }

    public Task<string?> SetAsideAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!File.Exists(_registryFile))
        {
            return Task.FromResult<string?>(null);
        }

        var backupFile = FirstFreeBackupName(now);

        try
        {
            File.Move(_registryFile, backupFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectRegistryWriteFailedException(_registryFile, exception);
        }

        return Task.FromResult<string?>(backupFile);
    }

    private string FirstFreeBackupName(DateTimeOffset now)
    {
        var baseName = $"{_registryFile}.corrupt-{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}";
        var candidate = baseName;

        for (var number = 2; File.Exists(candidate); number++)
        {
            candidate = $"{baseName}-{number}";
        }

        return candidate;
    }

    private bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), pathComparison);

    /// <summary>Drops trailing separators, but keeps a root such as "/" or "C:\" intact.</summary>
    private static string Normalize(string path)
    {
        var trimmed = path.TrimEnd(Separators);

        return trimmed.Length == 0 || trimmed.EndsWith(':') ? path : trimmed;
    }

    private List<RegisteredProject> Load()
    {
        if (!File.Exists(_registryFile))
        {
            return [];
        }

        var document = Deserialize(ReadFile());

        return document.Projects
            .Select(entry => new RegisteredProject(entry.Path, entry.DisplayName, entry.LastOpenedAt))
            .ToList();
    }

    private string ReadFile()
    {
        try
        {
            return File.ReadAllText(_registryFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectRegistryUnreadableException(_registryFile, exception);
        }
    }

    private RegistryDocument Deserialize(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<RegistryDocument>(json, JsonOptions);

            if (document?.Projects is null)
            {
                throw new ProjectRegistryUnreadableException(_registryFile);
            }

            return document;
        }
        catch (JsonException exception)
        {
            throw new ProjectRegistryUnreadableException(_registryFile, exception);
        }
    }

    private void Write(IEnumerable<RegisteredProject> projects)
    {
        var temporaryFile = _registryFile + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_registryFile)!);
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(ToDocument(projects), JsonOptions));
            File.Move(temporaryFile, _registryFile, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectRegistryWriteFailedException(_registryFile, exception);
        }
    }

    private static RegistryDocument ToDocument(IEnumerable<RegisteredProject> projects) => new()
    {
        Projects = projects
            .Select(project => new RegistryEntry
            {
                Path = project.Path,
                DisplayName = project.DisplayName,
                LastOpenedAt = project.LastOpenedAt,
            })
            .ToList(),
    };
}
