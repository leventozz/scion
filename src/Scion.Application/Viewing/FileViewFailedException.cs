namespace Scion.Application.Viewing;

/// <summary>A file adapter failure the Application layer turns into a <see cref="SkillViewError"/>.</summary>
public sealed class FileViewFailedException(Exception? innerException = null)
    : Exception("The files could not be read.", innerException);
