using System.Security.Cryptography;
using System.Text;
using Scion.Application.Editing;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Keeps the previous versions of edited files in Scion's own folder (<c>{scionHome}/.scion/backups</c>), never in the
/// user's repository. Each file has a folder named after it and a hash of its path, a <c>source.txt</c> with the
/// original path, and its last <see cref="KeptBackups"/> versions.
/// </summary>
public sealed class FileSystemFileBackups : IFileBackups
{
    private const int KeptBackups = 20;
    private const int PathHashLength = 16;
    private const string SourceFileName = "source.txt";
    private const string TempSuffix = ".tmp";

    private readonly string _scionHome;
    private readonly EditableFilePaths _paths;
    private readonly StringComparison _pathComparison;

    /// <param name="scionHome">Where <c>.scion/backups</c> lives.</param>
    /// <param name="homeDirectory">The root of Personal.</param>
    /// <param name="pathComparison">The file system's case rule, so one file never gets two backup folders.</param>
    public FileSystemFileBackups(
        string scionHome,
        string homeDirectory,
        IEnumerable<IAgentLayout> layouts,
        IEnumerable<IInstructionFileLayout> instructionLayouts,
        StringComparison pathComparison)
    {
        _scionHome = scionHome;
        _paths = new EditableFilePaths(homeDirectory, layouts, instructionLayouts);
        _pathComparison = pathComparison;
    }

    public async Task KeepPreviousVersionAsync(EditableFile file, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var source = _paths.PathOf(file);
        if (!File.Exists(source))
        {
            return;
        }

        string folder;
        try
        {
            folder = Path.Combine(_scionHome, ".scion", "backups", FolderNameOf(source, _pathComparison));
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, SourceFileName), source, cancellationToken);
            await CopyAsync(source, UniqueBackupPath(folder, now, Path.GetFileName(source)), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileBackupFailedException(exception);
        }

        DeleteOldBackups(folder);
    }

    internal static string FolderNameOf(string absolutePath, StringComparison comparison)
    {
        var ignoresCase = comparison is StringComparison.OrdinalIgnoreCase
            or StringComparison.InvariantCultureIgnoreCase
            or StringComparison.CurrentCultureIgnoreCase;
        var key = ignoresCase ? absolutePath.ToLowerInvariant() : absolutePath;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..PathHashLength];

        return $"{Path.GetFileName(absolutePath)}-{hash}";
    }

    private static string UniqueBackupPath(string folder, DateTimeOffset now, string fileName)
    {
        var name = $"{now.UtcDateTime:yyyyMMddTHHmmssfff}Z-{fileName}";
        var path = Path.Combine(folder, name);

        for (var number = 2; File.Exists(path); number++)
        {
            path = Path.Combine(folder, $"{name}-{number}");
        }

        return path;
    }

    // Another process may keep the file open for writing, so share it like the reader does.
    private static async Task CopyAsync(string source, string backupPath, CancellationToken cancellationToken)
    {
        var temp = backupPath + TempSuffix;

        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            File.Move(temp, backupPath);
        }
        catch
        {
            BestEffort.DeleteFile(temp);
            throw;
        }
    }

    // Names start with a sortable UTC timestamp, so the oldest sort first.
    private static void DeleteOldBackups(string folder)
    {
        try
        {
            var backups = Directory.EnumerateFiles(folder)
                .Where(file => Path.GetFileName(file) != SourceFileName && !file.EndsWith(TempSuffix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();

            foreach (var old in backups.SkipLast(KeptBackups))
            {
                File.Delete(old);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: the new backup is safe, and the next save prunes again.
        }
    }
}
