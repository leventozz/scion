using Scion.Application.Adapting;
using Scion.Domain;

namespace Scion.UI.Tests;

/// <summary>Answers with what a test decided and records the prompts; can be held so a test sees the running state.</summary>
internal sealed class FakeSkillAdapter : ISkillAdapter
{
    private Result<string, AdaptationFailure> _answer =
        Result<string, AdaptationFailure>.Success("---\nname: other-name\ndescription: Adapted for the target.\n---\n# Adapted\n");

    private TaskCompletionSource? _gate;
    private AdaptationProgress[] _reports = [];

    public List<AdaptationPrompt> Prompts { get; } = [];

    public bool WasCancelled { get; private set; }

    public void Returns(string skillFile) => _answer = Result<string, AdaptationFailure>.Success(skillFile);

    public void FailsWith(AdaptationFailure failure) => _answer = Result<string, AdaptationFailure>.Failure(failure);

    /// <summary>What it reports to the caller before it waits (if held) and answers.</summary>
    public void ReportsBeforeAnswering(params AdaptationProgress[] reports) => _reports = reports;

    /// <summary>The adapter waits until the returned source is completed (or the token is cancelled).</summary>
    public TaskCompletionSource Hold() => _gate = new TaskCompletionSource();

    public async Task<Result<string, AdaptationFailure>> AdaptAsync(
        AdaptationPrompt prompt, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        foreach (var report in _reports)
        {
            progress.Report(report);
        }

        if (_gate is { } gate)
        {
            try
            {
                await gate.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                return Result<string, AdaptationFailure>.Failure(new AdaptationFailure.Cancelled());
            }
        }

        return _answer;
    }
}
