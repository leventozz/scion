namespace Scion.Application.Updating;

/// <param name="Message">What went wrong, as far as it could be told.</param>
public sealed record UpdateError(string Message);
