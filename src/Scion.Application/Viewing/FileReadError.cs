namespace Scion.Application.Viewing;

/// <summary>Why the start of a file could not be read: it is missing, locked or not permitted.</summary>
public enum FileReadError
{
    Unreadable,
    Missing,
}
