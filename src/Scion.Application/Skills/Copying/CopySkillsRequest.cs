namespace Scion.Application.Skills.Copying;

/// <param name="Resolutions">
/// What to do with each folder name the target already has. A conflict without an entry is skipped.
/// </param>
public sealed record CopySkillsRequest(
    AgentLocation Source,
    IReadOnlyList<string> FolderNames,
    AgentLocation Target,
    IReadOnlyDictionary<string, ConflictResolution> Resolutions);
