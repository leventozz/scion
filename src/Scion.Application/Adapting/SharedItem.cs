namespace Scion.Application.Adapting;

/// <summary>One line of "What will be shared": what the user reads and, word for word, what is sent.</summary>
/// <param name="Bytes">The size to show next to the label; null for an item that has no size to show.</param>
public sealed record SharedItem(string Label, string Text, int? Bytes);
