namespace Scion.Infrastructure.FileSystem;

/// <summary>A directory cannot be moved because source and target are on different volumes; copy it instead.</summary>
internal sealed class DirectoryMoveAcrossVolumesException(Exception? innerException = null)
    : IOException("The directory cannot be moved to another volume.", innerException);
