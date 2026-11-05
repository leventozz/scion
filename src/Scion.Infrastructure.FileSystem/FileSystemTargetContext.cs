using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Reads what Scion may tell an AI provider about a target: the agent's instruction files, the project manifests on an
/// allowlist and the names of the folders two levels deep. Nothing else is ever opened, so no source file is read.
/// Links are not followed. A Personal target is the user's home folder: only its instruction file is read.
/// </summary>
public sealed class FileSystemTargetContext(string homeDirectory, IEnumerable<IInstructionFileLayout> layouts) : ITargetContextReader
{
    /// <summary>How many levels of folder names an adaptation shares; the README and the website say the same.</summary>
    public const int FolderNameDepth = 2;

    /// <summary>The project manifests an adaptation shares: file names, or <c>*.extension</c> patterns. The one list the README and the website are tested against.</summary>
    public static IReadOnlyList<string> SharedManifests { get; } =
    [
        "*.csproj", "*.sln", "*.slnx", "package.json", "pyproject.toml", "go.mod", "Cargo.toml", "pom.xml", "build.gradle",
    ];

    private const string ExtensionPattern = "*";

    private static readonly HashSet<string> ManifestNames = SharedManifests
        .Where(manifest => !manifest.StartsWith(ExtensionPattern, StringComparison.Ordinal))
        .ToHashSet(StringComparer.Ordinal);

    private static readonly string[] ManifestExtensions = SharedManifests
        .Where(manifest => manifest.StartsWith(ExtensionPattern, StringComparison.Ordinal))
        .Select(manifest => manifest[ExtensionPattern.Length..])
        .ToArray();

    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj",
    };

    public async Task<TargetContext> ReadAsync(AgentLocation target, ContextLimits limits, CancellationToken cancellationToken)
    {
        var root = LocationRoot.Of(target.Location, homeDirectory);
        var files = await ReadInstructionFilesAsync(root, target, limits, cancellationToken);

        if (target.Location is Location.Personal || !Directory.Exists(root))
        {
            return new TargetContext(files, []);
        }

        var tree = Walk(root);
        foreach (var manifest in tree.Manifests)
        {
            if (await ReadFileAsync(root, manifest, TargetContextFileKind.Manifest, limits, cancellationToken) is { } file)
            {
                files.Add(file);
            }
        }

        return new TargetContext(files, tree.FolderNames);
    }

    private async Task<List<TargetContextFile>> ReadInstructionFilesAsync(
        string root, AgentLocation target, ContextLimits limits, CancellationToken cancellationToken)
    {
        var layout = layouts.Single(candidate => candidate.Agent == target.Agent);
        var conventionPaths = target.Location is Location.Personal ? layout.PersonalInstructionFiles : layout.ProjectInstructionFiles;
        var files = new List<TargetContextFile>();

        foreach (var relativePath in conventionPaths)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (ContainsFileNamed(Path.GetDirectoryName(path)!, Path.GetFileName(path))
                && await ReadFileAsync(root, path, TargetContextFileKind.InstructionFile, limits, cancellationToken) is { } file)
            {
                files.Add(file);
            }
        }

        return files;
    }

    // File.Exists is case-insensitive on Windows, so compare the listed names ourselves (as the viewer does).
    private static bool ContainsFileNamed(string directory, string fileName) =>
        Directory.Exists(directory)
        && Directory.EnumerateFiles(directory)
            .Any(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.Ordinal));

    private static async Task<TargetContextFile?> ReadFileAsync(
        string root, string path, TargetContextFileKind kind, ContextLimits limits, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).LinkTarget is not null)
        {
            return null;
        }

        var read = await FileHead.ReadAsync(path, limits.MaxFileBytes, cancellationToken);

        return read.IsSuccess
            ? new TargetContextFile(Path.GetRelativePath(root, path).Replace('\\', '/'), kind, read.Value.Head)
            : null;
    }

    private static ProjectTree Walk(string root)
    {
        var manifests = new List<string>();
        var folderNames = new List<string>();

        void Visit(string directory, int level)
        {
            manifests.AddRange(Safely(() => Directory.EnumerateFiles(directory)).Where(IsManifest));

            if (level == FolderNameDepth)
            {
                return;
            }

            foreach (var child in Safely(() => Directory.EnumerateDirectories(directory)).Where(IsWalked).Order(StringComparer.Ordinal))
            {
                folderNames.Add(Path.GetRelativePath(root, child).Replace('\\', '/'));
                Visit(child, level + 1);
            }
        }

        Visit(root, 0);
        folderNames.Sort(StringComparer.Ordinal);

        return new ProjectTree(manifests, folderNames);
    }

    private static bool IsManifest(string path)
    {
        var name = Path.GetFileName(path);

        return ManifestNames.Contains(name)
            || ManifestExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWalked(string directory)
    {
        var name = Path.GetFileName(directory);

        return !name.StartsWith('.') && !SkippedFolders.Contains(name) && !FolderLinks.IsLink(directory);
    }

    /// <summary>A folder that cannot be listed is left out; the rest of the project is still described.</summary>
    private static IEnumerable<string> Safely(Func<IEnumerable<string>> list)
    {
        try
        {
            return list().ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private sealed record ProjectTree(IReadOnlyList<string> Manifests, IReadOnlyList<string> FolderNames);
}
