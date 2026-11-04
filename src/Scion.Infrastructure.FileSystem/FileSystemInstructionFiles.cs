using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemInstructionFiles(string homeDirectory, IEnumerable<IInstructionFileLayout> layouts) : IInstructionFiles
{
    public async Task<IReadOnlyList<InstructionFileBytes>> ReadAsync(
        Location location, Agent agent, int maxBytes, CancellationToken cancellationToken)
    {
        var layout = layouts.Single(candidate => candidate.Agent == agent);
        var root = LocationRoot.Of(location, homeDirectory);
        var conventionPaths = location is Location.Personal ? layout.PersonalInstructionFiles : layout.ProjectInstructionFiles;

        var files = new List<InstructionFileBytes>();
        foreach (var relativePath in conventionPaths)
        {
            var filePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(filePath)!;

            if (ContainsFileNamed(directory, Path.GetFileName(filePath)))
            {
                files.Add(new InstructionFileBytes(relativePath, directory, await FileHead.ReadAsync(filePath, maxBytes, cancellationToken)));
            }
        }

        return files;
    }

    // File.Exists is case-insensitive on Windows, so compare the listed names ourselves (as for SKILL.md).
    private static bool ContainsFileNamed(string directory, string fileName) =>
        Directory.Exists(directory)
        && Directory.EnumerateFiles(directory)
            .Any(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.Ordinal));
}
