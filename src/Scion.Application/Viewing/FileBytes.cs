namespace Scion.Application.Viewing;

/// <summary>
/// The start of a file and its real length. A port reads at most <c>maxBytes + 1</c> bytes, so a huge file is never
/// loaded and <see cref="Length"/> still tells whether it was cut.
/// </summary>
public sealed record FileBytes(byte[] Head, long Length);
