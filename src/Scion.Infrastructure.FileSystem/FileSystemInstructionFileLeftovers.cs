using Scion.Application.Editing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

public sealed class FileSystemInstructionFileLeftovers : IInstructionFileLeftovers
{
    private readonly string _homeDirectory;
    private readonly IReadOnlyList<IInstructionFileLayout> _layouts;

    public FileSystemInstructionFileLeftovers(string homeDirectory, IEnumerable<IInstructionFileLayout> layouts)
    {
        _homeDirectory = homeDirectory;
        _layouts = [.. layouts];
    }

    public Task CleanUpAsync(Location location, CancellationToken cancellationToken) =>
        Task.Run(() => CleanUp(location), cancellationToken);

    private void CleanUp(Location location)
    {
        var root = LocationRoot.Of(location, _homeDirectory);
        var conventions = _layouts.SelectMany(layout =>
            location is Location.Personal ? layout.PersonalInstructionFiles : layout.ProjectInstructionFiles);

        foreach (var instructionFile in conventions.Distinct(StringComparer.Ordinal))
        {
            var target = Path.Combine(root, instructionFile.Replace('/', Path.DirectorySeparatorChar));
            RemoveLeftoversBeside(target);
        }
    }

    private static void RemoveLeftoversBeside(string target)
    {
        var folder = Path.GetDirectoryName(target)!;
        var targetName = Path.GetFileName(target);

        try
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder).ToList())
            {
                if (InstructionTempFile.IsLeftoverOf(targetName, Path.GetFileName(file)))
                {
                    BestEffort.DeleteFile(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: whatever is left is tried again at the next start.
        }
    }
}
