using Scion.Application.Skills.Copying;

namespace Scion.UI;

/// <summary>What the user has picked in the copy dialog: the checked skills, their conflicts and how each is resolved.</summary>
internal sealed class CopySelection(PathComparison pathComparison)
{
    private readonly HashSet<string> _checked = [];
    private readonly Dictionary<string, ConflictResolution> _resolutions = [];

    public AgentLocation? Source { get; set; }

    public AgentLocation? Target { get; set; }

    /// <summary>Folder names that the target has with different content, as reported by the preview.</summary>
    public IReadOnlyCollection<string> DifferentInTarget { get; set; } = [];

    /// <summary>Folder names that the target has with identical content, as reported by the preview.</summary>
    public IReadOnlyCollection<string> IdenticalInTarget { get; set; } = [];

    public bool SameSourceAndTarget =>
        Source is { } source
        && Target is { } target
        && source.Agent == target.Agent
        && pathComparison.Same(source.Location, target.Location);

    public bool HasConflicts => _checked.Any(IsConflict);

    public int CopyCount => _checked.Count(WillBeWritten);

    public int OverwrittenCount => _checked.Count(folder => IsConflict(folder) && WillBeWritten(folder));

    public int SkippedCount => _checked.Count(folder => IsConflict(folder) && !WillBeWritten(folder));

    public int IdenticalCount => _checked.Count(IsIdentical);

    public bool CanCopy => Source is not null && Target is not null && !SameSourceAndTarget && CopyCount > 0;

    /// <summary>Plain "Copy" while nothing would be written; the button is disabled then.</summary>
    public string CopyLabel => CopyCount switch
    {
        0 => "Copy",
        1 => "Copy 1 skill",
        _ => $"Copy {CopyCount} skills",
    };

    public IReadOnlyDictionary<string, ConflictResolution> Resolutions =>
        _resolutions.Where(entry => IsConflict(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value);

    public bool IsChecked(string folder) => _checked.Contains(folder);

    public bool IsConflict(string folder) => IsChecked(folder) && DifferentInTarget.Contains(folder);

    public bool IsIdentical(string folder) => IsChecked(folder) && IdenticalInTarget.Contains(folder);

    public ConflictResolution ResolutionOf(string folder) =>
        _resolutions.GetValueOrDefault(folder, ConflictResolution.Skip);

    public void Toggle(string folder)
    {
        if (!_checked.Remove(folder))
        {
            _checked.Add(folder);
        }
    }

    public void CheckAll(IEnumerable<string> folders) => _checked.UnionWith(folders);

    public void Uncheck(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            _checked.Remove(folder);
            _resolutions.Remove(folder);
        }
    }

    public void ForgetResolutions() => _resolutions.Clear();

    public void Clear()
    {
        _checked.Clear();
        _resolutions.Clear();
    }

    public void Resolve(string folder, ConflictResolution resolution) => _resolutions[folder] = resolution;

    public void ResolveAllConflicts(ConflictResolution resolution)
    {
        foreach (var folder in _checked.Where(IsConflict))
        {
            _resolutions[folder] = resolution;
        }
    }

    public string Summary(string targetName)
    {
        if (Source is null)
        {
            return "";
        }

        if (SameSourceAndTarget)
        {
            return "Choose a different project or agent.";
        }

        if (Target is null)
        {
            return "Choose a project to copy to.";
        }

        var summary = $"{CopyCount} {(CopyCount == 1 ? "skill" : "skills")} will be copied to {targetName}";
        if (SkippedCount > 0)
        {
            summary += $", {SkippedCount} skipped";
        }

        if (OverwrittenCount > 0)
        {
            summary += $", {OverwrittenCount} overwritten";
        }

        if (IdenticalCount > 0)
        {
            summary += $", {IdenticalCount} already identical";
        }

        return summary + ".";
    }

    private bool WillBeWritten(string folder) =>
        !IsIdentical(folder) && (!IsConflict(folder) || ResolutionOf(folder) == ConflictResolution.Overwrite);
}
