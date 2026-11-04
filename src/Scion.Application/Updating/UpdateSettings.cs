namespace Scion.Application.Updating;

/// <param name="CheckWhenScionStarts">Whether Scion looks for a newer version each time it starts.</param>
public sealed record UpdateSettings(bool CheckWhenScionStarts);
