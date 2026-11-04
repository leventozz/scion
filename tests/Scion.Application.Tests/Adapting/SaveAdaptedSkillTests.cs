using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Projects;
using Scion.Application.Tests.Skills.Copying;

namespace Scion.Application.Tests.Adapting;

public class SaveAdaptedSkillTests
{
    private static readonly AdaptationRequest Request = AdaptationRequests.Sample();

    private readonly InMemorySkillFolders _folders = new();
    private readonly InMemoryAdaptedSkillWriter _writer;
    private readonly InMemoryFolderProbe _probe = new();

    public SaveAdaptedSkillTests() => _writer = new InMemoryAdaptedSkillWriter(_folders);

    private SaveAdaptedSkill SaveWith(StringComparison comparison = StringComparison.Ordinal) =>
        new(_folders, _writer, _probe, new PathComparison(comparison));

    [Fact]
    public async Task Copies_the_skill_with_the_adapted_text_when_the_target_does_not_have_it()
    {
        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SkillCopyOutcome.Copied, result.Value);
        var written = Assert.Single(_writer.Written);
        Assert.False(written.Replaced);
        Assert.Equal((Request.Source, "docker", Request.Target), (written.Source, written.FolderName, written.Target));
        Assert.Equal("adapted\n", Encoding.UTF8.GetString(written.SkillFile));
    }

    [Fact]
    public async Task Writes_the_text_with_the_BOM_and_the_line_ending_of_the_source()
    {
        var request = Request with { SourceFormat = new TextFormat(true, LineEnding.CrLf) };

        await SaveWith().ExecuteAsync(request, "a\nb\n", ConflictResolution.Skip);

        Assert.Equal(
            (byte[])[0xEF, 0xBB, 0xBF, .. "a\r\nb\r\n"u8],
            Assert.Single(_writer.Written).SkillFile);
    }

    [Fact]
    public async Task Does_not_write_when_the_target_has_the_skill_and_the_user_did_not_choose_to_overwrite()
    {
        _folders.Add(Request.Target, "docker");

        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SaveAdaptedSkillError.AlreadyExists, result.Error);
        Assert.Empty(_writer.Written);
    }

    [Fact]
    public async Task Replaces_the_skill_when_the_user_chose_to_overwrite()
    {
        _folders.Add(Request.Target, "docker");

        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Overwrite);

        Assert.Equal(SkillCopyOutcome.Overwritten, result.Value);
        Assert.True(Assert.Single(_writer.Written).Replaced);
    }

    [Fact]
    public async Task Copies_when_overwrite_was_chosen_but_the_skill_is_gone_by_now()
    {
        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Overwrite);

        Assert.Equal(SkillCopyOutcome.Copied, result.Value);
        Assert.False(Assert.Single(_writer.Written).Replaced);
    }

    [Fact]
    public async Task Reads_the_names_when_saving_so_a_skill_that_appeared_since_the_review_is_not_overwritten()
    {
        var save = SaveWith();
        _folders.Add(Request.Target, "docker"); // appears after the review, before the save

        var result = await save.ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SaveAdaptedSkillError.AlreadyExists, result.Error);
    }

    [Fact]
    public async Task Compares_the_folder_names_the_way_the_file_system_does()
    {
        _folders.Add(Request.Target, "Docker");

        var caseSensitive = await SaveWith(StringComparison.Ordinal).ExecuteAsync(Request, "x", ConflictResolution.Skip);
        var caseInsensitive = await SaveWith(StringComparison.OrdinalIgnoreCase).ExecuteAsync(Request, "x", ConflictResolution.Skip);

        Assert.True(caseSensitive.IsSuccess);
        Assert.Equal(SaveAdaptedSkillError.AlreadyExists, caseInsensitive.Error);
    }

    [Fact]
    public async Task Writes_nothing_when_the_target_folder_is_gone()
    {
        _probe.MarkMissing("/work/web");

        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SaveAdaptedSkillError.TargetFolderMissing, result.Error);
        Assert.Empty(_writer.Written);
    }

    [Fact]
    public async Task Reports_a_failed_write()
    {
        _writer.Fails();

        var result = await SaveWith().ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SaveAdaptedSkillError.Failed, result.Error);
    }

    [Fact]
    public async Task A_folder_that_appears_between_the_check_and_the_copy_is_a_failure_and_not_an_overwrite()
    {
        var racingFolders = new AppearsAfterListing(_folders);
        var save = new SaveAdaptedSkill(racingFolders, _writer, _probe, new PathComparison(StringComparison.Ordinal));

        var result = await save.ExecuteAsync(Request, "adapted\n", ConflictResolution.Skip);

        Assert.Equal(SaveAdaptedSkillError.Failed, result.Error);
        Assert.Empty(_writer.Written);
    }

    /// <summary>Lists nothing, then lets the folder appear: another program creating the skill at the wrong moment.</summary>
    private sealed class AppearsAfterListing(InMemorySkillFolders folders) : ISkillFolderNames
    {
        public async Task<IReadOnlyList<string>> ListAsync(AgentLocation place, CancellationToken cancellationToken)
        {
            var names = await folders.ListAsync(place, cancellationToken);
            folders.Add(place, "docker");

            return names;
        }
    }
}
