namespace Scion.Application.Updating;

/// <summary>An update source could not be reached or did not make sense. The message is for the user to read.</summary>
public sealed class UpdateFailedException(string message) : Exception(message);
