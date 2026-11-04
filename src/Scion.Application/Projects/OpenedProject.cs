namespace Scion.Application.Projects;

public sealed record OpenedProject(string Path, string DisplayName, bool FolderMissing = false);
