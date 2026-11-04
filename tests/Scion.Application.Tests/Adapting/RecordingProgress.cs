using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>Keeps what is reported, in order. Synchronous, unlike <see cref="Progress{T}"/>, so a test can read it at once.</summary>
internal sealed class RecordingProgress : IProgress<AdaptationProgress>
{
    public List<AdaptationProgress> Reports { get; } = [];

    public IEnumerable<string> Texts => Reports.OfType<AdaptationProgress.TextArrived>().Select(report => report.Text);

    public void Report(AdaptationProgress value) => Reports.Add(value);
}
