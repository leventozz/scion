using System.Text;
using Scion.Application.Editing;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Adapting;

/// <summary>
/// Everything an adaptation is made of, and the single source of truth for what is shared: the dialog lists
/// <see cref="SharedItems"/> and the prompt is built from exactly those items.
/// </summary>
/// <param name="SkillName">The name the adapted skill keeps: the source's <c>name</c>, or its folder name without one.</param>
/// <param name="SourceText">The SKILL.md to adapt, with LF line breaks.</param>
/// <param name="SourceFormat">The BOM and line ending the adapted SKILL.md is written with.</param>
/// <param name="TargetName">How the user knows the target (a project name or "Personal").</param>
/// <param name="OmittedItems">What would have been shared but was left out, and why. Never part of <see cref="SharedItems"/> or the prompt.</param>
public sealed record AdaptationRequest(
    AgentLocation Source,
    string SourceFolderName,
    string SkillName,
    string SourceText,
    TextFormat SourceFormat,
    AgentLocation Target,
    string TargetName,
    IReadOnlyList<SharedFile> Files,
    IReadOnlyList<string> FolderNames,
    string ExtraInstructions,
    IReadOnlyList<OmittedItem> OmittedItems)
{
    public IReadOnlyList<SharedItem> SharedItems
    {
        get
        {
            var items = new List<SharedItem>
            {
                new($"SKILL.md from {SourceFolderName}", SourceText, Encoding.UTF8.GetByteCount(SourceText)),
            };

            items.AddRange(Files.Select(file => new SharedItem(file.Label, file.Text, file.Bytes)));

            if (FolderNames.Count > 0)
            {
                items.Add(new SharedItem(SharedItemLabels.FolderNames(TargetName), FolderNamesText.Of(FolderNames), null));
            }

            if (!string.IsNullOrWhiteSpace(ExtraInstructions))
            {
                items.Add(new SharedItem("Your extra instructions", ExtraInstructions, null));
            }

            return items;
        }
    }

    /// <summary>
    /// The same items in short: the skill, the files of a project counted in one line (a Personal target has one file,
    /// which keeps its own line), the folder names, the extra instructions.
    /// </summary>
    public IReadOnlyList<SharedSummaryLine> Summary
    {
        get
        {
            var items = SharedItems;
            var skill = items[0];
            var files = items.Skip(1).Take(Files.Count).ToList();
            var rest = items.Skip(1 + Files.Count);

            var lines = new List<SharedSummaryLine> { new SharedSummaryLine.Item(skill) };

            if (Target.Location is Location.Personal)
            {
                lines.AddRange(files.Select(file => new SharedSummaryLine.Item(file)));
            }
            else if (files.Count > 0)
            {
                lines.Add(new SharedSummaryLine.ProjectFiles(files.Count, files.Sum(file => file.Bytes ?? 0)));
            }

            lines.AddRange(rest.Select(item => new SharedSummaryLine.Item(item)));

            return lines;
        }
    }

    public AdaptationRequest WithExtraInstructions(string extraInstructions) =>
        this with { ExtraInstructions = extraInstructions };
}
