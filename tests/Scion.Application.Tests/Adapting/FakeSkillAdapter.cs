using Scion.Application.Adapting;
using Scion.Domain;

namespace Scion.Application.Tests.Adapting;

/// <summary>Records every prompt it gets and answers with what a test decided; or waits until it is cancelled.</summary>
internal sealed class FakeSkillAdapter : ISkillAdapter
{
    private Func<CancellationToken, Task<Result<string, AdaptationFailure>>> _answer =
        _ => Task.FromResult(Result<string, AdaptationFailure>.Success("---\nname: other\ndescription: Adapted\n---\n# Adapted\n"));

    private AdaptationProgress[] _reports = [];

    public List<AdaptationPrompt> Prompts { get; } = [];

    /// <summary>What it reports, in order, before it answers.</summary>
    public void ReportsBeforeAnswering(params AdaptationProgress[] reports) => _reports = reports;

    public void Returns(string skillFile) =>
        _answer = _ => Task.FromResult(Result<string, AdaptationFailure>.Success(skillFile));

    public void FailsWith(AdaptationFailure failure) =>
        _answer = _ => Task.FromResult(Result<string, AdaptationFailure>.Failure(failure));

    /// <summary>Never finishes on its own; when cancelled it reports <see cref="AdaptationFailure.Cancelled"/> like an adapter should.</summary>
    public void Hangs() => _answer = async cancellationToken =>
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        return Result<string, AdaptationFailure>.Failure(new AdaptationFailure.Cancelled());
    };

    /// <summary>Never finishes on its own; when cancelled it throws, which an adapter must not do but a test may check.</summary>
    public void HangsAndThrowsWhenCancelled() => _answer = async cancellationToken =>
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("unreachable");
    };

    public Task<Result<string, AdaptationFailure>> AdaptAsync(
        AdaptationPrompt prompt, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        foreach (var report in _reports)
        {
            progress.Report(report);
        }

        return _answer(cancellationToken);
    }
}
