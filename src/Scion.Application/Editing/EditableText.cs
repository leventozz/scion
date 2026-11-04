namespace Scion.Application.Editing;

/// <param name="Text">With LF line breaks only, so that a draft is compared to it with a plain string comparison.</param>
public sealed record EditableText(string Text, EditBaseline Baseline);
