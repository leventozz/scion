using System.Text.Json;
using System.Text.Json.Serialization;
using Scion.Application.Deleting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

/// <summary>
/// Moves skill folders to Scion's trash, <c>.scion/trash</c> under the Scion home. An entry is the folder itself
/// ("{UTC time}-{folder}") with a metadata file beside it ("{entry}.json") that says where it came from. Nothing is
/// ever deleted without a copy being in the trash first. Linked folders are never touched.
/// </summary>
public sealed class FileSystemSkillTrash : ISkillTrash
{
    private const string IncomingPrefix = ".incoming-";

    private static readonly JsonSerializerOptions MetadataFormat = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _trashRoot;
    private readonly string _homeDirectory;
    private readonly SkillsDirectories _skillsDirectories;
    private readonly TrashSteps _steps;

    public FileSystemSkillTrash(string scionHome, string homeDirectory, IEnumerable<IAgentLayout> layouts)
        : this(scionHome, homeDirectory, layouts, TrashSteps.Real)
    {
    }

    internal FileSystemSkillTrash(
        string scionHome, string homeDirectory, IEnumerable<IAgentLayout> layouts, TrashSteps steps)
    {
        _trashRoot = Path.Combine(scionHome, ".scion", "trash");
        _homeDirectory = homeDirectory;
        _skillsDirectories = new SkillsDirectories(homeDirectory, layouts);
        _steps = steps;
    }

    public async Task<TrashedSkill> MoveToTrashAsync(
        AgentLocation place, string folderName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        SkillFolderName.EnsureIsSingleSegment(folderName, _ => new SkillTrashFailedException(SkillTrashFailure.Other));

        var skillsDirectory = _skillsDirectories.For(place.Location, place.Agent);
        var skillFolder = Path.Combine(skillsDirectory, folderName);
        if (!Directory.Exists(skillFolder))
        {
            throw new SkillTrashFailedException(SkillTrashFailure.Missing);
        }

        if (FolderLinks.IsLink(skillFolder) || FolderLinks.IsInLinkedSkillsArea(skillsDirectory))
        {
            throw new SkillTrashFailedException(SkillTrashFailure.Linked);
        }

        try
        {
            return await Task.Run(() => MoveToTrash(place, skillFolder, folderName, now), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SkillTrashFailedException(SkillTrashFailure.Other, exception);
        }
    }

    private TrashedSkill MoveToTrash(AgentLocation place, string skillFolder, string folderName, DateTimeOffset now)
    {
        Directory.CreateDirectory(_trashRoot);

        var entry = FirstFreeEntry($"{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{folderName}");
        var metadataFile = entry + ".json";
        WriteMetadata(metadataFile, place, skillFolder, now);

        try
        {
            return new TrashedSkill(MoveInto(skillFolder, entry));
        }
        catch
        {
            BestEffort.DeleteFile(metadataFile);
            throw;
        }
    }

    /// <returns>Whether the original folder is gone afterwards.</returns>
    private bool MoveInto(string skillFolder, string entry)
    {
        try
        {
            _steps.MoveDirectory(skillFolder, entry);
            return true;
        }
        catch (DirectoryMoveAcrossVolumesException)
        {
            CopyInto(skillFolder, entry);
            return RemoveOriginal(skillFolder);
        }
    }

    /// <summary>Copies into a staging folder first, so the trash never shows a half-copied entry.</summary>
    private void CopyInto(string skillFolder, string entry)
    {
        var staging = Path.Combine(_trashRoot, $"{IncomingPrefix}{Guid.NewGuid():N}");

        try
        {
            _steps.CopyTree(skillFolder, staging);
            _steps.MoveDirectory(staging, entry);
        }
        catch
        {
            BestEffort.Run(() => _steps.DeleteTree(staging));
            throw;
        }
    }

    /// <summary>The skill is safe in the trash by now, so a removal that stops halfway is a result, not a failure.</summary>
    private bool RemoveOriginal(string skillFolder)
    {
        try
        {
            _steps.DeleteTree(skillFolder);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string FirstFreeEntry(string baseName)
    {
        var candidate = baseName;

        for (var number = 2; Taken(candidate); number++)
        {
            candidate = $"{baseName}-{number}";
        }

        return Path.Combine(_trashRoot, candidate);

        bool Taken(string name) =>
            Directory.Exists(Path.Combine(_trashRoot, name)) || File.Exists(Path.Combine(_trashRoot, name + ".json"));
    }

    private void WriteMetadata(string metadataFile, AgentLocation place, string skillFolder, DateTimeOffset now)
    {
        var metadata = new TrashMetadata(
            skillFolder,
            place.Location switch
            {
                Location.Project project => new TrashLocation("project", project.Path),
                _ => new TrashLocation("personal", null),
            },
            JsonNamingPolicy.CamelCase.ConvertName(place.Agent.ToString()),
            now.UtcDateTime);

        var temporaryFile = metadataFile + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(metadata, MetadataFormat));

        try
        {
            File.Move(temporaryFile, metadataFile, overwrite: false);
        }
        catch
        {
            BestEffort.DeleteFile(temporaryFile);
            throw;
        }
    }

    private sealed record TrashMetadata(string OriginalPath, TrashLocation Location, string Agent, DateTime DeletedAt);

    private sealed record TrashLocation(string Kind, string? Path);
}
