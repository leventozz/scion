using Scion.Application.Adapting;

namespace Scion.Infrastructure.ClaudeCode.Tests;

/// <summary>Keeps what is reported, in order. Reports come from the thread that reads the program's output.</summary>
internal sealed class RecordingProgress : IProgress<AdaptationProgress>
{
    private readonly List<AdaptationProgress> _reports = [];

    public IReadOnlyList<AdaptationProgress> Reports
    {
        get
        {
            lock (_reports)
            {
                return [.. _reports];
            }
        }
    }

    public IEnumerable<string> Texts => Reports.OfType<AdaptationProgress.TextArrived>().Select(report => report.Text);

    public void Report(AdaptationProgress value)
    {
        lock (_reports)
        {
            _reports.Add(value);
        }
    }
}
