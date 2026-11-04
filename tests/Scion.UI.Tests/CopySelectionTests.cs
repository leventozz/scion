using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

public class CopySelectionTests
{
    private static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);
    private static readonly AgentLocation Web = new(new Location.Project("/work/web"), Agent.ClaudeCode);

    private readonly CopySelection _selection = new(new PathComparison(StringComparison.Ordinal))
    {
        Source = Api,
        Target = Web,
    };

    private void Check(params string[] folders)
    {
        foreach (var folder in folders)
        {
            _selection.Toggle(folder);
        }
    }

    [Fact]
    public void Toggle_checks_and_unchecks_a_folder()
    {
        _selection.Toggle("docker");
        Assert.True(_selection.IsChecked("docker"));

        _selection.Toggle("docker");
        Assert.False(_selection.IsChecked("docker"));
    }

    [Fact]
    public void Select_all_checks_every_given_folder()
    {
        _selection.CheckAll(["docker", "testing"]);

        Assert.True(_selection.IsChecked("docker"));
        Assert.True(_selection.IsChecked("testing"));
    }

    [Fact]
    public void Only_a_checked_folder_that_exists_in_the_target_is_a_conflict()
    {
        _selection.DifferentInTarget = ["docker", "testing"];
        Check("docker", "deploy");

        Assert.True(_selection.IsConflict("docker"));
        Assert.False(_selection.IsConflict("testing"));
        Assert.False(_selection.IsConflict("deploy"));
        Assert.True(_selection.HasConflicts);
    }

    [Fact]
    public void Has_no_conflicts_without_a_checked_conflicting_folder()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("deploy");

        Assert.False(_selection.HasConflicts);
    }

    [Fact]
    public void A_conflict_is_skipped_until_it_is_resolved_with_overwrite()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("docker");

        Assert.Equal(ConflictResolution.Skip, _selection.ResolutionOf("docker"));

        _selection.Resolve("docker", ConflictResolution.Overwrite);

        Assert.Equal(ConflictResolution.Overwrite, _selection.ResolutionOf("docker"));
    }

    [Fact]
    public void Resolving_all_applies_to_every_checked_conflict_only()
    {
        _selection.DifferentInTarget = ["docker", "testing", "lint"];
        Check("docker", "testing");

        _selection.ResolveAllConflicts(ConflictResolution.Overwrite);

        Assert.Equal(ConflictResolution.Overwrite, _selection.ResolutionOf("docker"));
        Assert.Equal(ConflictResolution.Overwrite, _selection.ResolutionOf("testing"));
        Assert.Equal(ConflictResolution.Skip, _selection.ResolutionOf("lint"));
    }

    [Fact]
    public void Counts_new_skills_and_overwrites_as_copied_and_skips_as_skipped()
    {
        _selection.DifferentInTarget = ["docker", "testing"];
        Check("docker", "testing", "deploy", "lint");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        Assert.Equal(3, _selection.CopyCount);
        Assert.Equal(1, _selection.SkippedCount);
        Assert.Equal(1, _selection.OverwrittenCount);
    }

    [Fact]
    public void Requests_only_the_resolutions_of_checked_conflicts()
    {
        _selection.DifferentInTarget = ["docker", "testing"];
        Check("docker");
        _selection.Resolve("docker", ConflictResolution.Overwrite);
        _selection.Resolve("testing", ConflictResolution.Overwrite);

        Assert.Equal(
            new Dictionary<string, ConflictResolution> { ["docker"] = ConflictResolution.Overwrite },
            _selection.Resolutions);
    }

    [Fact]
    public void Can_copy_when_something_will_be_written_to_a_different_target()
    {
        Check("docker");

        Assert.True(_selection.CanCopy);
    }

    [Fact]
    public void Cannot_copy_when_nothing_will_be_written()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("docker");

        Assert.False(_selection.CanCopy);
    }

    [Fact]
    public void Cannot_copy_without_a_source_or_a_target()
    {
        Check("docker");

        _selection.Target = null;
        Assert.False(_selection.CanCopy);

        _selection.Target = Web;
        _selection.Source = null;
        Assert.False(_selection.CanCopy);
    }

    [Fact]
    public void Cannot_copy_from_a_place_to_itself()
    {
        _selection.Target = Api;
        Check("docker");

        Assert.True(_selection.SameSourceAndTarget);
        Assert.False(_selection.CanCopy);
    }

    [Fact]
    public void The_same_project_with_another_agent_is_a_different_place()
    {
        _selection.Target = Api with { Agent = Agent.Codex };

        Assert.False(_selection.SameSourceAndTarget);
    }

    [Fact]
    public void A_checked_folder_with_identical_content_is_neither_a_conflict_nor_copied()
    {
        _selection.IdenticalInTarget = ["docker"];
        Check("docker", "deploy");

        Assert.True(_selection.IsIdentical("docker"));
        Assert.False(_selection.IsIdentical("deploy"));
        Assert.False(_selection.IsConflict("docker"));
        Assert.False(_selection.HasConflicts);
        Assert.Equal(1, _selection.CopyCount);
        Assert.Equal(0, _selection.SkippedCount);
        Assert.Equal(1, _selection.IdenticalCount);
    }

    [Fact]
    public void Cannot_copy_when_everything_checked_is_identical()
    {
        _selection.IdenticalInTarget = ["docker"];
        Check("docker");

        Assert.False(_selection.CanCopy);
    }

    [Fact]
    public void Does_not_request_a_resolution_for_an_identical_folder()
    {
        _selection.IdenticalInTarget = ["docker"];
        Check("docker");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        Assert.Empty(_selection.Resolutions);
    }

    [Fact]
    public void Unchecks_the_given_folders_and_forgets_their_resolutions()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("docker", "testing");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        _selection.Uncheck(["docker"]);

        Assert.False(_selection.IsChecked("docker"));
        Assert.True(_selection.IsChecked("testing"));
        Assert.Equal(ConflictResolution.Skip, _selection.ResolutionOf("docker"));
    }

    [Fact]
    public void Forgetting_resolutions_keeps_the_checks()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("docker");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        _selection.ForgetResolutions();

        Assert.True(_selection.IsChecked("docker"));
        Assert.Equal(ConflictResolution.Skip, _selection.ResolutionOf("docker"));
    }

    [Fact]
    public void Clearing_forgets_checks_and_resolutions()
    {
        _selection.DifferentInTarget = ["docker"];
        Check("docker");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        _selection.Clear();

        Assert.False(_selection.IsChecked("docker"));
        Assert.Equal(ConflictResolution.Skip, _selection.ResolutionOf("docker"));
    }

    [Theory]
    [InlineData(0, 0, 0, "0 skills will be copied to web.")]
    [InlineData(1, 0, 0, "1 skill will be copied to web.")]
    [InlineData(3, 0, 0, "3 skills will be copied to web.")]
    [InlineData(1, 2, 0, "1 skill will be copied to web, 2 skipped.")]
    [InlineData(2, 0, 1, "2 skills will be copied to web, 1 overwritten.")]
    [InlineData(3, 1, 2, "3 skills will be copied to web, 1 skipped, 2 overwritten.")]
    public void Summarises_what_a_copy_will_do(int newSkills, int skipped, int overwritten, string expected)
    {
        var folders = new List<string>();
        var conflicts = new List<string>();
        for (var i = 0; i < newSkills - overwritten; i++) folders.Add($"new-{i}");
        for (var i = 0; i < skipped; i++) conflicts.Add($"skip-{i}");
        for (var i = 0; i < overwritten; i++) conflicts.Add($"over-{i}");
        _selection.DifferentInTarget = conflicts;
        Check([.. folders, .. conflicts]);
        foreach (var name in conflicts.Where(name => name.StartsWith("over-")))
        {
            _selection.Resolve(name, ConflictResolution.Overwrite);
        }

        Assert.Equal(expected, _selection.Summary("web"));
    }

    [Theory]
    [InlineData(1, 1, "1 skill will be copied to web, 1 already identical.")]
    [InlineData(0, 3, "0 skills will be copied to web, 3 already identical.")]
    public void Counts_identical_skills_in_the_summary(int newSkills, int identical, string expected)
    {
        _selection.IdenticalInTarget = [.. Enumerable.Range(0, identical).Select(i => $"same-{i}")];
        Check([.. Enumerable.Range(0, newSkills).Select(i => $"new-{i}"), .. _selection.IdenticalInTarget]);

        Assert.Equal(expected, _selection.Summary("web"));
    }

    [Fact]
    public void Lists_identical_after_skipped_and_overwritten_in_the_summary()
    {
        _selection.DifferentInTarget = ["docker", "testing"];
        _selection.IdenticalInTarget = ["lint"];
        Check("docker", "testing", "lint");
        _selection.Resolve("docker", ConflictResolution.Overwrite);

        Assert.Equal(
            "1 skill will be copied to web, 1 skipped, 1 overwritten, 1 already identical.",
            _selection.Summary("web"));
    }

    [Fact]
    public void Asks_for_a_different_place_when_source_and_target_are_the_same()
    {
        _selection.Target = Api;

        Assert.Equal("Choose a different project or agent.", _selection.Summary("api"));
    }

    [Fact]
    public void Asks_for_a_target_when_there_is_none()
    {
        _selection.Target = null;

        Assert.Equal("Choose a project to copy to.", _selection.Summary(""));
    }

    [Fact]
    public void Says_nothing_while_there_is_no_source()
    {
        _selection.Source = null;

        Assert.Equal("", _selection.Summary("web"));
    }

    [Theory]
    [InlineData(0, "Copy")]
    [InlineData(1, "Copy 1 skill")]
    [InlineData(2, "Copy 2 skills")]
    public void Labels_the_copy_button_with_the_number_of_skills(int count, string expected)
    {
        for (var i = 0; i < count; i++)
        {
            Check($"skill-{i}");
        }

        Assert.Equal(expected, _selection.CopyLabel);
    }
}
