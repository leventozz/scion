namespace Scion.UI;

/// <summary>
/// Reports on the thread that reports, at once. Unlike <see cref="Progress{T}"/> it does not post to a captured
/// context: the dialog decides how a report reaches the renderer.
/// </summary>
internal sealed class ActionProgress<T>(Action<T> onReport) : IProgress<T>
{
    public void Report(T value) => onReport(value);
}
