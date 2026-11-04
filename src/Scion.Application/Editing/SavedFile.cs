namespace Scion.Application.Editing;

/// <param name="NewBaseline">For what was just written, so the next save compares against it.</param>
/// <param name="BackupSkipped">The previous version could not be backed up, but the file was saved.</param>
public sealed record SavedFile(EditBaseline NewBaseline, bool BackupSkipped);
