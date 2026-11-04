using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Skills.Copying;

public class CopySkillsTests
{
    private static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);
    private static readonly AgentLocation Web = new(new Location.Project("/work/web"), Agent.ClaudeCode);

    private readonly InMemoryFolderProbe _probe = new();
    private readonly InMemorySkillFolders _folders = new();
    private readonly CopySkills _copySkills;

    public CopySkillsTests() => _copySkills = CopySkillsWith(StringComparison.Ordinal);

    private CopySkills CopySkillsWith(StringComparison comparison) =>
        new(_probe, _folders, _folders, _folders, new PathComparison(comparison));

    private static CopySkillsRequest Request(
        AgentLocation source,
        AgentLocation target,
        string[] folderNames,
        Dictionary<string, ConflictResolution>? resolutions = null) =>
        new(source, folderNames, target, resolutions ?? []);

    [Fact]
    public async Task Copies_skills_that_the_target_does_not_have()
    {
        _folders.Add(Api, "docker", "testing");

        var result = await _copySkills.ExecuteAsync(Request(Api, Web, ["docker", "testing"]));

        Assert.Equal(
            [new CopiedSkill("docker", SkillCopyOutcome.Copied), new CopiedSkill("testing", SkillCopyOutcome.Copied)],
            result.Value.Skills);
        Assert.True(_folders.Has(Web, "docker"));
        Assert.True(_folders.Has(Web, "testing"));
    }

    [Fact]
    public async Task Works_in_every_direction_from_personal_to_a_project_and_across_agents()
    {
        var personalCodex = new AgentLocation(new Location.Personal(), Agent.Codex);
        var webCodex = Web with { Agent = Agent.Codex };
        _folders.Add(personalCodex, "docker");

        var toProject = await _copySkills.ExecuteAsync(Request(personalCodex, Web, ["docker"]));
        var otherAgent = await _copySkills.ExecuteAsync(Request(Web, webCodex, ["docker"]));

        Assert.True(toProject.IsSuccess);
        Assert.Equal(SkillCopyOutcome.Copied, otherAgent.Value.Skills.Single().Outcome);
        Assert.True(_folders.Has(webCodex, "docker"));
    }

    [Fact]
    public async Task Skips_a_conflict_that_has_no_resolution_and_leaves_the_target_untouched()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");

        var result = await _copySkills.ExecuteAsync(Request(Api, Web, ["docker"]));

        Assert.Equal(new CopiedSkill("docker", SkillCopyOutcome.Skipped), result.Value.Skills.Single());
        Assert.Empty(_folders.Replaced);
        Assert.Empty(_folders.Copied);
    }

    [Fact]
    public async Task Skips_a_conflict_that_was_resolved_with_skip()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");

        var result = await _copySkills.ExecuteAsync(
            Request(Api, Web, ["docker"], new() { ["docker"] = ConflictResolution.Skip }));

        Assert.Equal(SkillCopyOutcome.Skipped, result.Value.Skills.Single().Outcome);
        Assert.Empty(_folders.Replaced);
    }

    [Fact]
    public async Task Replaces_a_conflict_that_was_resolved_with_overwrite()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");

        var result = await _copySkills.ExecuteAsync(
            Request(Api, Web, ["docker"], new() { ["docker"] = ConflictResolution.Overwrite }));

        Assert.Equal(new CopiedSkill("docker", SkillCopyOutcome.Overwritten), result.Value.Skills.Single());
        Assert.Equal(["docker"], _folders.Replaced);
    }

    [Fact]
    public async Task Leaves_a_skill_alone_whose_content_is_identical_even_when_overwrite_was_chosen()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");
        _folders.SetContent(Api, "docker", "same");
        _folders.SetContent(Web, "docker", "same");

        var result = await _copySkills.ExecuteAsync(
            Request(Api, Web, ["docker"], new() { ["docker"] = ConflictResolution.Overwrite }));

        Assert.Equal(new CopiedSkill("docker", SkillCopyOutcome.Identical), result.Value.Skills.Single());
        Assert.Empty(_folders.Replaced);
    }

    [Fact]
    public async Task Treats_a_skill_that_cannot_be_compared_like_one_with_different_content()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");
        _folders.SetContent(Api, "docker", "same");
        _folders.SetContent(Web, "docker", "same");
        _folders.FailToCompare("docker");

        var result = await _copySkills.ExecuteAsync(
            Request(Api, Web, ["docker"], new() { ["docker"] = ConflictResolution.Overwrite }));

        Assert.Equal(SkillCopyOutcome.Overwritten, result.Value.Skills.Single().Outcome);
    }

    [Fact]
    public async Task Decides_each_skill_on_its_own()
    {
        _folders.Add(Api, "docker", "testing", "deploy");
        _folders.Add(Web, "docker", "testing");

        var result = await _copySkills.ExecuteAsync(Request(
            Api,
            Web,
            ["docker", "testing", "deploy"],
            new() { ["docker"] = ConflictResolution.Overwrite }));

        Assert.Equal(
            [
                new CopiedSkill("docker", SkillCopyOutcome.Overwritten),
                new CopiedSkill("testing", SkillCopyOutcome.Skipped),
                new CopiedSkill("deploy", SkillCopyOutcome.Copied),
            ],
            result.Value.Skills);
    }

    [Fact]
    public async Task Marks_a_skill_as_failed_and_carries_on_with_the_others()
    {
        _folders.Add(Api, "docker", "broken", "testing");
        _folders.FailOn("broken");

        var result = await _copySkills.ExecuteAsync(Request(Api, Web, ["docker", "broken", "testing"]));

        Assert.Equal(
            [SkillCopyOutcome.Copied, SkillCopyOutcome.Failed, SkillCopyOutcome.Copied],
            result.Value.Skills.Select(skill => skill.Outcome));
        Assert.True(_folders.Has(Web, "testing"));
    }

    [Fact]
    public async Task Marks_a_failed_overwrite_as_failed()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "docker");
        _folders.FailOn("docker");

        var result = await _copySkills.ExecuteAsync(
            Request(Api, Web, ["docker"], new() { ["docker"] = ConflictResolution.Overwrite }));

        Assert.Equal(SkillCopyOutcome.Failed, result.Value.Skills.Single().Outcome);
    }

    [Fact]
    public async Task Copies_a_duplicated_selection_only_once()
    {
        _folders.Add(Api, "Docker");

        var result = await CopySkillsWith(StringComparison.OrdinalIgnoreCase)
            .ExecuteAsync(Request(Api, Web, ["Docker", "docker"]));

        Assert.Equal([new CopiedSkill("Docker", SkillCopyOutcome.Copied)], result.Value.Skills);
    }

    [Fact]
    public async Task Compares_the_target_names_with_the_injected_case_rule_when_looking_for_conflicts()
    {
        _folders.Add(Api, "docker");
        _folders.Add(Web, "Docker");

        var result = await CopySkillsWith(StringComparison.OrdinalIgnoreCase)
            .ExecuteAsync(Request(Api, Web, ["docker"]));

        Assert.Equal(SkillCopyOutcome.Skipped, result.Value.Skills.Single().Outcome);
    }

    [Fact]
    public async Task Copies_nothing_for_an_empty_selection()
    {
        var result = await _copySkills.ExecuteAsync(Request(Api, Web, []));

        Assert.Empty(result.Value.Skills);
    }

    [Fact]
    public async Task Refuses_the_same_location_and_agent_as_source_and_target()
    {
        _folders.Add(Api, "docker");

        var result = await _copySkills.ExecuteAsync(Request(Api, Api, ["docker"]));

        Assert.Equal(CopySkillsError.SameSourceAndTarget, result.Error);
    }

    [Fact]
    public async Task Recognises_the_same_project_through_the_path_case_rule()
    {
        var sameProject = new AgentLocation(new Location.Project("/work/API"), Agent.ClaudeCode);

        var result = await CopySkillsWith(StringComparison.OrdinalIgnoreCase)
            .ExecuteAsync(Request(Api, sameProject, ["docker"]));

        Assert.Equal(CopySkillsError.SameSourceAndTarget, result.Error);
    }

    [Fact]
    public async Task Fails_when_the_source_folder_is_missing_and_copies_nothing()
    {
        _folders.Add(Api, "docker");
        _probe.MarkMissing("/work/api");

        var result = await _copySkills.ExecuteAsync(Request(Api, Web, ["docker"]));

        Assert.Equal(CopySkillsError.SourceFolderMissing, result.Error);
        Assert.Empty(_folders.Copied);
    }

    [Fact]
    public async Task Fails_when_the_target_folder_is_missing_and_copies_nothing()
    {
        _folders.Add(Api, "docker");
        _probe.MarkMissing("/work/web");

        var result = await _copySkills.ExecuteAsync(Request(Api, Web, ["docker"]));

        Assert.Equal(CopySkillsError.TargetFolderMissing, result.Error);
        Assert.Empty(_folders.Copied);
    }
}
