namespace Scion.Application.Adapting;

/// <param name="Label">What the user reads in "What will be shared" and what titles the document in the prompt.</param>
/// <param name="Bytes">The size of the file on disk (BOM included), which is what the size limits count.</param>
public sealed record SharedFile(string Label, string Text, int Bytes);
