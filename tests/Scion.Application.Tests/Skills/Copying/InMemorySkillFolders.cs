using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Skills.Copying;

/// <summary>Both copying ports over a dictionary of folder names; behaves like the contract says.</summary>
internal sealed class InMemorySkillFolders : ISkillFolderNames, ISkillFolderCopier, ISkillFolderComparer
{
    private readonly Dictionary<AgentLocation, HashSet<string>> _names = new();
    private readonly Dictionary<(AgentLocation, string), string> _content = new();
    private readonly HashSet<string> _failing = [];
    private readonly HashSet<string> _failingToCompare = [];

    public List<string> Copied { get; } = [];

    public List<string> Replaced { get; } = [];

    public void Add(AgentLocation place, params string[] folderNames)
    {
        if (!_names.TryGetValue(place, out var names))
        {
            _names[place] = names = [];
        }

        names.UnionWith(folderNames);
        foreach (var folderName in folderNames)
        {
            // Every place gets its own content, so folders differ unless a test makes them identical.
            _content[(place, folderName)] = place.ToString();
        }
    }

    public void SetContent(AgentLocation place, string folderName, string content) =>
        _content[(place, folderName)] = content;

    public void FailToCompare(string folderName) => _failingToCompare.Add(folderName);

    public void FailOn(string folderName) => _failing.Add(folderName);

    public bool Has(AgentLocation place, string folderName) =>
        _names.TryGetValue(place, out var names) && names.Contains(folderName);

    public Task<IReadOnlyList<string>> ListAsync(AgentLocation place, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(_names.TryGetValue(place, out var names) ? [.. names] : []);

    public Task CopyAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        EnsureCopyable(source, folderName);
        if (Has(target, folderName))
        {
            throw new SkillCopyFailedException(folderName);
        }

        Add(target, folderName);
        SetContent(target, folderName, _content[(source, folderName)]);
        Copied.Add(folderName);
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        EnsureCopyable(source, folderName);
        Add(target, folderName);
        SetContent(target, folderName, _content[(source, folderName)]);
        Replaced.Add(folderName);
        return Task.CompletedTask;
    }

    public Task<bool> AreIdenticalAsync(
        AgentLocation source, string folderName, AgentLocation target, CancellationToken cancellationToken)
    {
        if (_failingToCompare.Contains(folderName)
            || !_content.TryGetValue((source, folderName), out var sourceContent)
            || !_content.TryGetValue((target, folderName), out var targetContent))
        {
            throw new SkillCopyFailedException(folderName);
        }

        return Task.FromResult(sourceContent == targetContent);
    }

    private void EnsureCopyable(AgentLocation source, string folderName)
    {
        if (_failing.Contains(folderName) || !Has(source, folderName))
        {
            throw new SkillCopyFailedException(folderName);
        }
    }
}
