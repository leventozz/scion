using Scion.Application.Editing;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Adapting;

public sealed class AdaptSkill(
    IAiProviderPorts<ISkillAdapter> skillAdapters,
    ISkillNameSetter skillNameSetter,
    CheckSkillFrontmatter checkSkillFrontmatter,
    ISkillFolderNames skillFolderNames,
    PathComparison pathComparison,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public async Task<Result<AdaptedSkill, AdaptationFailure>> ExecuteAsync(
        AiProviderId provider,
        AdaptationRequest request,
        IProgress<AdaptationProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var answer = await AskAsync(skillAdapters.For(provider), request, progress, cancellationToken);
        if (!answer.IsSuccess)
        {
            return Failure(answer.Error);
        }

        if (string.IsNullOrWhiteSpace(answer.Value))
        {
            return Failure(new AdaptationFailure.Failed("The answer was empty."));
        }

        var normalized = LineBreaks.Normalize(answer.Value);
        var text = skillNameSetter.WithName(normalized, request.SkillName) ?? normalized;
        var existing = await skillFolderNames.ListAsync(request.Target, cancellationToken);

        return Result<AdaptedSkill, AdaptationFailure>.Success(new AdaptedSkill(
            text,
            checkSkillFrontmatter.Execute(text, request.SourceFolderName),
            existing.Any(name => pathComparison.Same(name, request.SourceFolderName))));
    }

    /// <summary>Asks the adapter within the time limit. Who ended the wait decides the failure: the user, or the clock.</summary>
    private async Task<Result<string, AdaptationFailure>> AskAsync(
        ISkillAdapter skillAdapter, AdaptationRequest request, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, timeProvider);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        Result<string, AdaptationFailure> answer;
        try
        {
            answer = await skillAdapter.AdaptAsync(AdaptationPrompt.For(request), progress, either.Token);
        }
        catch (OperationCanceledException) when (either.IsCancellationRequested)
        {
            answer = Failure<string>(new AdaptationFailure.Cancelled());
        }

        if (!either.IsCancellationRequested)
        {
            return answer;
        }

        // The adapter was cancelled, by the user or by the clock; a result that still came in is not wanted.
        return Failure<string>(cancellationToken.IsCancellationRequested
            ? new AdaptationFailure.Cancelled()
            : new AdaptationFailure.TimedOut());
    }

    private static Result<AdaptedSkill, AdaptationFailure> Failure(AdaptationFailure failure) =>
        Result<AdaptedSkill, AdaptationFailure>.Failure(failure);

    private static Result<T, AdaptationFailure> Failure<T>(AdaptationFailure failure) =>
        Result<T, AdaptationFailure>.Failure(failure);
}
