using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Adapting;

/// <summary>Builds requests directly, so tests of the prompt and of the list do not depend on how one is prepared.</summary>
internal static class AdaptationRequests
{
    public static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);
    public static readonly AgentLocation Web = new(new Location.Project("/work/web"), Agent.ClaudeCode);

    public static AdaptationRequest Sample(
        IReadOnlyList<SharedFile>? files = null,
        IReadOnlyList<string>? folderNames = null,
        string extraInstructions = "",
        IReadOnlyList<OmittedItem>? omittedItems = null) =>
        new(
            Api,
            "docker",
            "docker",
            "---\nname: docker\n---\n# Docker\n",
            new TextFormat(false, LineEnding.Lf),
            Web,
            "Web",
            files ?? [],
            folderNames ?? [],
            extraInstructions,
            omittedItems ?? []);
}
