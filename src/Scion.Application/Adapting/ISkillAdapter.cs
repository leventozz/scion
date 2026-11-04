using Scion.Domain;

namespace Scion.Application.Adapting;

public interface ISkillAdapter
{
    /// <summary>
    /// Sends the prompt to the AI provider and returns the adapted SKILL.md text. Every expected problem is a
    /// <see cref="AdaptationFailure"/>; a cancelled token gives <see cref="AdaptationFailure.Cancelled"/> after the
    /// provider's process has been ended.
    /// </summary>
    /// <param name="progress">
    /// Receives the text while it is being written, for display only. The pieces are not verified and can be thrown away
    /// (<see cref="AdaptationProgress.StartedOver"/>); the result is always the complete text this method returns.
    /// </param>
    Task<Result<string, AdaptationFailure>> AdaptAsync(
        AdaptationPrompt prompt, IProgress<AdaptationProgress> progress, CancellationToken cancellationToken);
}
