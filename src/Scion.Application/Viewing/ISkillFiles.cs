using Scion.Domain;

namespace Scion.Application.Viewing;

public interface ISkillFiles
{
    /// <summary>
    /// The files of one skill folder as <c>/</c>-separated relative paths. Entries whose name starts with a dot are
    /// left out at every depth. A missing skill folder gives <c>null</c>; a folder that cannot be read throws
    /// <see cref="FileViewFailedException"/>.
    /// </summary>
    Task<SkillFolderListing?> ListAsync(Location location, Agent agent, string folderName, CancellationToken cancellationToken);

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> + 1 bytes of one file. A file that cannot be read (missing, locked,
    /// no permission) comes back as a failed <see cref="SkillFileContent.Read"/>. A folder name or a
    /// path that leaves the skill folder throws <see cref="FileViewFailedException"/>.
    /// </summary>
    Task<SkillFileContent> ReadAsync(
        Location location, Agent agent, string folderName, string relativePath, int maxBytes, CancellationToken cancellationToken);
}
