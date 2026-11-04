using Scion.Domain;

namespace Scion.UI;

public enum CopySide
{
    Source,
    Target,
}

/// <summary>
/// How the copy dialog opens: one side is fixed (the screen the user came from), the other is pre-filled or
/// left for the user to choose. The other side starts with the same agent unless said otherwise.
/// </summary>
public sealed record CopyDialogStart(
    CopySide FixedSide,
    CopyPlace FixedPlace,
    Agent FixedAgent,
    CopyPlace? OtherPlace,
    Agent OtherAgent,
    string? CheckedFolder = null)
{
    /// <summary>"Copy from…": the screen is the target, the source is still to be chosen.</summary>
    public static CopyDialogStart CopyFrom(CopyPlace target, Agent agent) =>
        new(CopySide.Target, target, agent, OtherPlace: null, OtherAgent: agent);

    /// <summary>"Copy from Codex": the screen is the target, the source is the same place with another agent.</summary>
    public static CopyDialogStart CopyFromAgent(CopyPlace target, Agent agent, Agent sourceAgent) =>
        new(CopySide.Target, target, agent, OtherPlace: target, OtherAgent: sourceAgent);

    /// <summary>"Copy to…" on a skill row: the skill is the source, the target is still to be chosen.</summary>
    public static CopyDialogStart CopyTo(CopyPlace source, Agent agent, string folderName) =>
        new(CopySide.Source, source, agent, OtherPlace: null, OtherAgent: agent, CheckedFolder: folderName);
}
