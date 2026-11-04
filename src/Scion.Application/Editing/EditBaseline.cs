namespace Scion.Application.Editing;

/// <param name="Hash">Of the bytes on disk when editing started; a save compares it to find outside changes.</param>
/// <param name="Format">The BOM and line ending to write back.</param>
public sealed record EditBaseline(string Hash, TextFormat Format);
