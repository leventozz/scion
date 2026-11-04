namespace Scion.UI;

/// <summary>What happened to the file on disk since editing started, when a save finds it out.</summary>
public enum DiskConflict
{
    Changed,
    Deleted,
}
