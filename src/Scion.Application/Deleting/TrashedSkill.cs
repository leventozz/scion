namespace Scion.Application.Deleting;

/// <param name="OriginalFullyRemoved">
/// False when the skill is safe in the trash but some of its files could not be removed from the skill folder.
/// </param>
public sealed record TrashedSkill(bool OriginalFullyRemoved);
