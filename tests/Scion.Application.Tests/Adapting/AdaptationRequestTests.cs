using System.Text;
using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class AdaptationRequestTests
{
    [Fact]
    public void Shares_the_skill_file_first_with_its_size()
    {
        var request = AdaptationRequests.Sample();

        var item = request.SharedItems[0];

        Assert.Equal("SKILL.md from docker", item.Label);
        Assert.Equal(request.SourceText, item.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount(request.SourceText), item.Bytes);
    }

    [Fact]
    public void Lists_every_shared_file_with_its_label_and_size_in_the_given_order()
    {
        var request = AdaptationRequests.Sample(files:
        [
            new SharedFile("CLAUDE.md from Web", "Use tabs.", 9),
            new SharedFile("Web.csproj", "<Project/>", 10),
        ]);

        Assert.Equal(
            ["SKILL.md from docker", "CLAUDE.md from Web", "Web.csproj"],
            request.SharedItems.Select(item => item.Label));
        Assert.Equal([9, 10], request.SharedItems.Skip(1).Select(item => item.Bytes));
    }

    [Fact]
    public void Shares_the_folder_names_as_one_item_without_a_size_when_there_are_any()
    {
        var request = AdaptationRequests.Sample(folderNames: ["src", "src/Web"]);

        var item = request.SharedItems.Last();

        Assert.Equal("Folder names in Web (two levels)", item.Label);
        Assert.Equal("src\nsrc/Web", item.Text);
        Assert.Null(item.Bytes);
    }

    [Fact]
    public void Shares_no_folder_names_item_when_there_are_none()
    {
        var request = AdaptationRequests.Sample();

        Assert.DoesNotContain(request.SharedItems, item => item.Label.StartsWith("Folder names"));
    }

    [Fact]
    public void Shares_the_extra_instructions_last_when_there_are_some()
    {
        var request = AdaptationRequests.Sample(folderNames: ["src"], extraInstructions: "We use PostgreSQL.");

        var item = request.SharedItems.Last();

        Assert.Equal("Your extra instructions", item.Label);
        Assert.Equal("We use PostgreSQL.", item.Text);
        Assert.Null(item.Bytes);
    }

    [Fact]
    public void Shares_no_extra_instructions_item_for_blank_instructions()
    {
        var request = AdaptationRequests.Sample(extraInstructions: "  \n ");

        Assert.DoesNotContain(request.SharedItems, item => item.Label == "Your extra instructions");
    }

    [Fact]
    public void What_is_not_shared_is_carried_by_the_request_but_is_never_one_of_the_shared_items()
    {
        var omitted = new OmittedItem("big.json", new OmittedReason.OverFileLimit(32768));
        var request = AdaptationRequests.Sample(
            files: [new SharedFile("small.json", "{}", 2)],
            omittedItems: [omitted]);

        Assert.Equal([omitted], request.OmittedItems);
        Assert.DoesNotContain(request.SharedItems, item => item.Label == "big.json");
        Assert.DoesNotContain(
            request.Summary.OfType<SharedSummaryLine.Item>(),
            line => line.Value.Label == "big.json");
        Assert.DoesNotContain("big.json", AdaptationPrompt.For(request).Content);
    }

    [Fact]
    public void With_extra_instructions_gives_a_new_request_and_leaves_the_first_one_alone()
    {
        var request = AdaptationRequests.Sample();

        var changed = request.WithExtraInstructions("Use PostgreSQL.");

        Assert.Equal("", request.ExtraInstructions);
        Assert.Equal("Use PostgreSQL.", changed.ExtraInstructions);
        Assert.Equal(request.SourceText, changed.SourceText);
    }
}
