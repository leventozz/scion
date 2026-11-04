namespace Scion.Application.Adapting;

/// <summary>
/// What an adapter sends: a fixed instruction and the shared items as titled documents. The content is built from
/// <see cref="AdaptationRequest.SharedItems"/> alone, so it can never hold more than the user was shown.
/// </summary>
public sealed record AdaptationPrompt(string Instructions, string Content)
{
    private const string Rules =
        "You adapt an agent skill to another project. The first document is the SKILL.md to adapt; the others describe "
        + "the target project (its instruction files, manifests and folder names) and may hold extra instructions from the user. "
        + "Each document starts with a line like <<< title >>>. "
        + "Rewrite the SKILL.md so that it fits the target project's stack and conventions. "
        + "Keep the `name` in the frontmatter exactly as it is. "
        + "Return the complete adapted SKILL.md only: the frontmatter and the body, with no explanation and no code fence around it.";

    /// <summary>The JSON schema both providers are asked to answer with: the complete skill file in one string.</summary>
    public const string AnswerSchema =
        "{\"type\":\"object\",\"properties\":{\"skillFile\":{\"type\":\"string\"}},\"required\":[\"skillFile\"],\"additionalProperties\":false}";

    public static AdaptationPrompt For(AdaptationRequest request)
    {
        var documents = request.SharedItems.Select(item => $"<<< {item.Label} >>>\n{item.Text}\n");

        return new AdaptationPrompt(Rules, string.Join('\n', documents));
    }
}
