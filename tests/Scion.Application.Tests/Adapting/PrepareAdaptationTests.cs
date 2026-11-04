using System.Text;
using Scion.Application.Adapting;
using Scion.Application.Editing;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Editing;
using Scion.Application.Tests.Projects;
using Scion.Domain;

namespace Scion.Application.Tests.Adapting;

public class PrepareAdaptationTests
{
    private static readonly AgentLocation Api = AdaptationRequests.Api;
    private static readonly AgentLocation Web = AdaptationRequests.Web;
    private static readonly AgentLocation Personal = new(new Location.Personal(), Agent.ClaudeCode);
    private static readonly EditableFile SourceSkillFile = new EditableFile.SkillFile(Api.Location, Api.Agent, "docker", "SKILL.md");

    private readonly InMemoryEditableFiles _files = new();
    private readonly InMemoryTargetContext _context = new();
    private readonly InMemoryFolderProbe _probe = new();
    private readonly PrepareAdaptation _prepare;

    public PrepareAdaptationTests()
    {
        _files.Put(SourceSkillFile, "---\nname: docker-tips\ndescription: Tips\n---\n# Docker\n");
        _prepare = new PrepareAdaptation(
            new OpenFileForEditing(_files),
            _context,
            new FakeFrontmatterParser(),
            _probe,
            new PathComparison(StringComparison.Ordinal));
    }

    private async Task<AdaptationRequest> PrepareAsync(AgentLocation? target = null, string targetName = "Web")
    {
        var result = await _prepare.ExecuteAsync(Api, "docker", target ?? Web, targetName);

        return result.Value;
    }

    [Fact]
    public async Task Reads_the_skill_file_and_remembers_its_format()
    {
        _files.Put(SourceSkillFile, (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("name: docker-tips\r\nBody")]);

        var request = await PrepareAsync();

        Assert.Equal("name: docker-tips\nBody", request.SourceText);
        Assert.Equal(new TextFormat(true, LineEnding.CrLf), request.SourceFormat);
        Assert.Equal(Api, request.Source);
        Assert.Equal("docker", request.SourceFolderName);
        Assert.Equal(Web, request.Target);
        Assert.Equal("Web", request.TargetName);
    }

    [Fact]
    public async Task Takes_the_skill_name_from_the_frontmatter()
    {
        var request = await PrepareAsync();

        Assert.Equal("docker-tips", request.SkillName);
    }

    [Fact]
    public async Task Falls_back_to_the_folder_name_when_the_frontmatter_has_no_name()
    {
        _files.Put(SourceSkillFile, "# Docker without a header\n");

        var request = await PrepareAsync();

        Assert.Equal("docker", request.SkillName);
    }

    [Fact]
    public async Task Fails_when_the_source_and_the_target_are_the_same_agent_in_the_same_place()
    {
        var result = await _prepare.ExecuteAsync(Api, "docker", Api, "Api");

        Assert.Equal(PrepareAdaptationError.SameSourceAndTarget, result.Error);
    }

    [Fact]
    public async Task Allows_the_other_agent_in_the_same_place()
    {
        var result = await _prepare.ExecuteAsync(Api, "docker", Api with { Agent = Agent.Codex }, "Api");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Fails_when_the_skill_file_is_missing()
    {
        _files.Delete(SourceSkillFile);

        var result = await _prepare.ExecuteAsync(Api, "docker", Web, "Web");

        Assert.Equal(PrepareAdaptationError.SourceUnreadable, result.Error);
    }

    [Fact]
    public async Task Fails_when_the_skill_file_is_not_text()
    {
        _files.Put(SourceSkillFile, (byte[])[0x00, 0x01, 0x02]);

        var result = await _prepare.ExecuteAsync(Api, "docker", Web, "Web");

        Assert.Equal(PrepareAdaptationError.SourceUnreadable, result.Error);
    }

    [Fact]
    public async Task Fails_when_the_target_project_folder_is_gone_and_reads_no_context()
    {
        _probe.MarkMissing("/work/web");

        var result = await _prepare.ExecuteAsync(Api, "docker", Web, "Web");

        Assert.Equal(PrepareAdaptationError.TargetFolderMissing, result.Error);
        Assert.Null(_context.AskedFor);
    }

    [Fact]
    public async Task Asks_the_reader_for_the_target_with_the_default_limits()
    {
        await PrepareAsync();

        Assert.Equal(Web, _context.AskedFor);
        Assert.Equal(ContextLimits.Default, _context.AskedWith);
    }

    [Fact]
    public async Task Shares_the_instruction_files_first_then_the_manifests_in_name_order_then_the_folder_names()
    {
        _context.AddManifest("src/Web/Web.csproj", "<Project/>");
        _context.AddManifest("package.json", "{}");
        _context.AddInstructionFile("CLAUDE.md", "Use tabs.");
        _context.AddFolderNames("src", "src/Web");

        var request = await PrepareAsync();

        Assert.Equal(
            [
                "SKILL.md from docker",
                "CLAUDE.md from Web",
                "package.json",
                "src/Web/Web.csproj",
                "Folder names in Web (two levels)",
            ],
            request.SharedItems.Select(item => item.Label));
    }

    [Fact]
    public async Task Gives_a_shared_file_its_text_and_its_size_in_bytes()
    {
        _context.AddInstructionFile("CLAUDE.md", "Tab → tabs.");

        var request = await PrepareAsync();

        var file = Assert.Single(request.Files);
        Assert.Equal("Tab → tabs.", file.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount("Tab → tabs."), file.Bytes);
    }

    [Fact]
    public async Task Never_shares_a_file_over_the_per_file_limit_and_never_cuts_it()
    {
        _context.AddManifest("big.json", new string('x', ContextLimits.Default.MaxFileBytes + 1));
        _context.AddManifest("small.json", "{}");

        var request = await PrepareAsync();

        Assert.Equal(["small.json"], request.Files.Select(file => file.Label));
        Assert.DoesNotContain(request.SharedItems, item => item.Label == "big.json");
        Assert.DoesNotContain("xxxxxxxx", AdaptationPrompt.For(request).Content);
    }

    [Fact]
    public async Task Shares_a_file_exactly_at_the_per_file_limit()
    {
        _context.AddManifest("exact.json", new string('x', ContextLimits.Default.MaxFileBytes));

        var request = await PrepareAsync();

        Assert.Equal(["exact.json"], request.Files.Select(file => file.Label));
    }

    [Fact]
    public async Task Skips_what_does_not_fit_in_the_total_and_keeps_the_order_of_what_does()
    {
        var full = ContextLimits.Default.MaxFileBytes;
        _context.AddInstructionFile("CLAUDE.md", new string('a', full));
        _context.AddManifest("a.json", new string('b', full));
        _context.AddManifest("b.json", new string('c', full));
        _context.AddManifest("c.json", new string('d', full - 768)); // 768 bytes of the total are left
        _context.AddManifest("d.json", new string('e', 1000)); // does not fit
        _context.AddManifest("e.json", "{}"); // fits again

        var request = await PrepareAsync();

        Assert.Equal(
            ["CLAUDE.md from Web", "a.json", "b.json", "c.json", "e.json"],
            request.Files.Select(file => file.Label));
    }

    [Fact]
    public async Task Counts_the_folder_names_toward_the_total_and_leaves_them_out_when_they_do_not_fit()
    {
        _context.AddInstructionFile("CLAUDE.md", new string('a', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("a.json", new string('b', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("b.json", new string('c', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("c.json", new string('d', ContextLimits.Default.MaxFileBytes - 1));
        _context.AddFolderNames("src", "src/Web"); // 11 bytes, the total has 1 byte left

        var request = await PrepareAsync();

        Assert.Equal(4, request.Files.Count);
        Assert.Empty(request.FolderNames);
    }

    [Fact]
    public async Task Lists_a_file_over_the_per_file_limit_as_not_shared_and_it_never_reaches_the_prompt()
    {
        _context.AddManifest("big.json", new string('x', ContextLimits.Default.MaxFileBytes + 1));
        _context.AddManifest("small.json", "{}");

        var request = await PrepareAsync();

        var omitted = Assert.Single(request.OmittedItems);
        Assert.Equal(new OmittedItem("big.json", new OmittedReason.OverFileLimit(ContextLimits.Default.MaxFileBytes)), omitted);
        Assert.DoesNotContain(request.SharedItems, item => item.Label == "big.json");
        var prompt = AdaptationPrompt.For(request).Content;
        Assert.DoesNotContain("big.json", prompt);
        Assert.DoesNotContain("xxxxxxxx", prompt);
    }

    [Fact]
    public async Task An_instruction_file_over_the_limit_is_listed_with_the_label_it_would_have_had()
    {
        _context.AddInstructionFile("CLAUDE.md", new string('x', ContextLimits.Default.MaxFileBytes + 1));

        var request = await PrepareAsync();

        Assert.Equal("CLAUDE.md from Web", Assert.Single(request.OmittedItems).Label);
    }

    [Fact]
    public async Task Lists_what_does_not_fit_in_the_total_as_not_shared_in_order_and_shares_what_does()
    {
        var full = ContextLimits.Default.MaxFileBytes;
        _context.AddInstructionFile("CLAUDE.md", new string('a', full));
        _context.AddManifest("a.json", new string('b', full));
        _context.AddManifest("b.json", new string('c', full));
        _context.AddManifest("c.json", new string('d', full - 768)); // 768 bytes of the total are left
        _context.AddManifest("d.json", new string('e', 1000)); // does not fit
        _context.AddManifest("e.json", "{}"); // fits again
        _context.AddManifest("f.json", new string('f', 2000)); // does not fit

        var request = await PrepareAsync();

        Assert.Equal(
            [
                new OmittedItem("d.json", new OmittedReason.TotalLimitReached()),
                new OmittedItem("f.json", new OmittedReason.TotalLimitReached()),
            ],
            request.OmittedItems);
        Assert.Equal(["CLAUDE.md from Web", "a.json", "b.json", "c.json", "e.json"], request.Files.Select(file => file.Label));
        Assert.DoesNotContain("eeeeeeee", AdaptationPrompt.For(request).Content);
    }

    [Fact]
    public async Task Lists_the_folder_names_as_not_shared_when_they_do_not_fit_in_the_total()
    {
        _context.AddInstructionFile("CLAUDE.md", new string('a', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("a.json", new string('b', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("b.json", new string('c', ContextLimits.Default.MaxFileBytes));
        _context.AddManifest("c.json", new string('d', ContextLimits.Default.MaxFileBytes - 1));
        _context.AddFolderNames("src", "src/Web");

        var request = await PrepareAsync();

        Assert.Equal(
            [new OmittedItem("Folder names in Web (two levels)", new OmittedReason.TotalLimitReached())],
            request.OmittedItems);
        Assert.Empty(request.FolderNames);
    }

    [Fact]
    public async Task Lists_nothing_as_not_shared_when_everything_fits()
    {
        _context.AddInstructionFile("CLAUDE.md", "Use tabs.");
        _context.AddManifest("a.json", "{}");
        _context.AddFolderNames("src");

        var request = await PrepareAsync();

        Assert.Empty(request.OmittedItems);
    }

    [Fact]
    public async Task A_file_that_is_not_text_is_not_listed_as_not_shared_because_it_was_never_a_candidate()
    {
        _context.AddManifest("data.json", [0x00, 0x01, 0x02]);

        var request = await PrepareAsync();

        Assert.Empty(request.OmittedItems);
    }

    [Fact]
    public async Task Leaves_out_a_file_that_is_not_text()
    {
        _context.AddManifest("data.json", [0x00, 0x01, 0x02]);
        _context.AddManifest("ok.json", "{}");

        var request = await PrepareAsync();

        Assert.Equal(["ok.json"], request.Files.Select(file => file.Label));
    }

    [Fact]
    public async Task Shares_a_file_with_a_BOM_without_it_but_counts_its_bytes()
    {
        var bytes = (byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{}")];
        _context.AddManifest("bom.json", bytes);

        var request = await PrepareAsync();

        var file = Assert.Single(request.Files);
        Assert.Equal("{}", file.Text);
        Assert.Equal(5, file.Bytes);
    }

    [Fact]
    public async Task A_personal_target_lists_only_its_own_instruction_file_as_not_shared_never_the_manifests_the_reader_gave()
    {
        _context.AddInstructionFile(".claude/CLAUDE.md", new string('x', ContextLimits.Default.MaxFileBytes + 1));
        _context.AddManifest("package.json", new string('y', ContextLimits.Default.MaxFileBytes + 1));

        var request = await PrepareAsync(Personal, "Personal");

        Assert.Equal(".claude/CLAUDE.md from Personal", Assert.Single(request.OmittedItems).Label);
    }

    [Fact]
    public async Task A_personal_target_shares_only_its_instruction_file_even_if_the_reader_gives_more()
    {
        _context.AddInstructionFile(".claude/CLAUDE.md", "My habits.");
        _context.AddManifest("package.json", "{}");
        _context.AddFolderNames("Documents", "Downloads");

        var request = await PrepareAsync(Personal, "Personal");

        Assert.Equal(
            ["SKILL.md from docker", ".claude/CLAUDE.md from Personal"],
            request.SharedItems.Select(item => item.Label));
        Assert.Empty(request.FolderNames);
    }

    [Fact]
    public async Task Starts_without_extra_instructions()
    {
        var request = await PrepareAsync();

        Assert.Equal("", request.ExtraInstructions);
    }
}
