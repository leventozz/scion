namespace Scion.Application.Skills.Copying;

/// <summary>What a copy would run into: selected folders that the target has too, split by whether the content matches.</summary>
public sealed record CopyPreview(IReadOnlyList<string> Different, IReadOnlyList<string> Identical);
