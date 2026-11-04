using System.Text.RegularExpressions;
using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

/// <summary>
/// The list the user reads ("What will be shared") and the prompt that is sent come from the same request. These tests
/// are the guard that they cannot drift apart.
/// </summary>
public class AdaptationPromptTests
{
    private static readonly Regex DocumentTitle = new(@"^<<< (.+) >>>$", RegexOptions.Multiline);

    private static string[] TitlesIn(AdaptationPrompt prompt) =>
        DocumentTitle.Matches(prompt.Content).Select(match => match.Groups[1].Value).ToArray();

    private static AdaptationRequest FullRequest() => AdaptationRequests.Sample(
        files:
        [
            new SharedFile("CLAUDE.md from Web", "Use tabs.", 9),
            new SharedFile("src/Web/Web.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"/>", 40),
        ],
        folderNames: ["src", "src/Web"],
        extraInstructions: "We use PostgreSQL.",
        omittedItems:
        [
            new OmittedItem("big.json", new OmittedReason.OverFileLimit(32768)),
            new OmittedItem("late.json", new OmittedReason.TotalLimitReached()),
        ]);

    [Fact]
    public void Every_document_in_the_prompt_is_in_the_shared_list_in_the_same_order()
    {
        var request = FullRequest();

        var titles = TitlesIn(AdaptationPrompt.For(request));

        Assert.Equal(request.SharedItems.Select(item => item.Label), titles);
    }

    [Fact]
    public void Every_shared_item_puts_its_content_in_the_prompt()
    {
        var request = FullRequest();

        var content = AdaptationPrompt.For(request).Content;

        foreach (var item in request.SharedItems)
        {
            Assert.Contains(item.Text, content);
        }
    }

    [Fact]
    public void The_prompt_and_the_list_change_together_when_the_extra_instructions_change()
    {
        var request = AdaptationRequests.Sample();
        var changed = request.WithExtraInstructions("Prefer small steps.");

        var titles = TitlesIn(AdaptationPrompt.For(changed));

        Assert.Equal(changed.SharedItems.Select(item => item.Label), titles);
        Assert.Contains("Prefer small steps.", AdaptationPrompt.For(changed).Content);
        Assert.DoesNotContain("Prefer small steps.", AdaptationPrompt.For(request).Content);
    }

    [Fact]
    public void Nothing_but_the_shared_items_goes_into_the_content()
    {
        var request = FullRequest();

        var content = AdaptationPrompt.For(request).Content;

        var expected = string.Join(
            "\n",
            request.SharedItems.Select(item => $"<<< {item.Label} >>>\n{item.Text}\n"));
        Assert.Equal(expected, content);
    }

    [Fact]
    public void What_is_not_shared_is_never_counted_as_sent_and_its_name_is_not_in_the_prompt()
    {
        var request = FullRequest();

        var prompt = AdaptationPrompt.For(request);

        Assert.Equal(2, request.OmittedItems.Count);
        Assert.Equal(request.SharedItems.Select(item => item.Label), TitlesIn(prompt));
        Assert.All(request.OmittedItems, omitted => Assert.DoesNotContain(omitted.Label, prompt.Content));
        Assert.All(request.OmittedItems, omitted => Assert.DoesNotContain(omitted.Label, prompt.Instructions));
    }

    [Fact]
    public void The_instructions_are_the_same_for_every_request_and_ask_for_the_complete_skill_file_only()
    {
        var first = AdaptationPrompt.For(AdaptationRequests.Sample());
        var second = AdaptationPrompt.For(FullRequest());

        Assert.Equal(first.Instructions, second.Instructions);
        Assert.Contains("complete", first.Instructions);
        Assert.Contains("SKILL.md", first.Instructions);
        Assert.Contains("name", first.Instructions);
    }

    [Fact]
    public void The_answer_schema_asks_for_exactly_one_string_the_skill_file()
    {
        using var schema = System.Text.Json.JsonDocument.Parse(AdaptationPrompt.AnswerSchema);
        var root = schema.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Equal("string", root.GetProperty("properties").GetProperty("skillFile").GetProperty("type").GetString());
        Assert.Equal(["skillFile"], root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }
}
