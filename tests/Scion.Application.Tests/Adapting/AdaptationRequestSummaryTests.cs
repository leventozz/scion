using Scion.Application.Adapting;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Adapting;

public class AdaptationRequestSummaryTests
{
    private static readonly AgentLocation Personal = new(new Location.Personal(), Agent.ClaudeCode);

    private static AdaptationRequest ProjectRequest(
        IReadOnlyList<SharedFile>? files = null,
        IReadOnlyList<string>? folderNames = null,
        string extraInstructions = "") =>
        AdaptationRequests.Sample(files, folderNames, extraInstructions);

    private static AdaptationRequest PersonalRequest(IReadOnlyList<SharedFile> files) =>
        AdaptationRequests.Sample(files) with { Target = Personal, TargetName = "Personal" };

    [Fact]
    public void A_project_target_sums_its_files_up_in_one_line_between_the_skill_and_the_folder_names()
    {
        var request = ProjectRequest(
            files: [new SharedFile("CLAUDE.md from Web", "a", 3000), new SharedFile("Web.csproj", "b", 4000)],
            folderNames: ["src"]);

        Assert.Equal(
            [
                new SharedSummaryLine.Item(request.SharedItems[0]),
                new SharedSummaryLine.ProjectFiles(2, 7000),
                new SharedSummaryLine.Item(request.SharedItems[^1]),
            ],
            request.Summary);
    }

    [Fact]
    public void A_project_target_without_files_has_no_files_line()
    {
        var request = ProjectRequest(folderNames: ["src"]);

        Assert.DoesNotContain(request.Summary, line => line is SharedSummaryLine.ProjectFiles);
        Assert.Equal(2, request.Summary.Count);
    }

    [Fact]
    public void A_personal_target_shows_its_file_as_a_line_of_its_own()
    {
        var request = PersonalRequest([new SharedFile("CLAUDE.md from Personal", "habits", 3000)]);

        Assert.Equal(
            [
                new SharedSummaryLine.Item(request.SharedItems[0]),
                new SharedSummaryLine.Item(request.SharedItems[1]),
            ],
            request.Summary);
    }

    [Fact]
    public void Extra_instructions_are_the_last_line()
    {
        var request = ProjectRequest(
            files: [new SharedFile("Web.csproj", "b", 4000)],
            folderNames: ["src"],
            extraInstructions: "We use PostgreSQL.");

        Assert.Equal(4, request.Summary.Count);
        Assert.Equal(new SharedSummaryLine.Item(request.SharedItems[^1]), request.Summary[^1]);
    }

    [Fact]
    public void Every_shared_item_is_represented_exactly_once()
    {
        var files = new[] { new SharedFile("a", "x", 10), new SharedFile("b", "y", 20), new SharedFile("c", "z", 30) };
        var request = ProjectRequest(files, ["src"], "More.");

        var items = request.Summary.OfType<SharedSummaryLine.Item>().Select(line => line.Value).ToList();
        var folded = request.Summary.OfType<SharedSummaryLine.ProjectFiles>().Single();

        Assert.Equal(request.SharedItems.Count, items.Count + folded.Count);
        Assert.Equal(60, folded.Bytes);
        Assert.Equal(3, folded.Count);
        Assert.All(items, item => Assert.Contains(item, request.SharedItems));
        Assert.DoesNotContain(items, item => files.Any(file => file.Label == item.Label));
    }
}
