namespace Scion.Infrastructure.Codex.Tests;

public class CodexEventsTests
{
    private int _toolCallbacks;

    private CodexEvents Events() => new(() => _toolCallbacks++);

    private static string Completed(string itemJson) => $"{{\"type\":\"item.completed\",\"item\":{itemJson}}}";

    private static string AgentMessage(string text) =>
        Completed($"{{\"id\":\"item_1\",\"type\":\"agent_message\",\"text\":{System.Text.Json.JsonSerializer.Serialize(text)}}}");

    private static void ReadAll(CodexEvents events, params string[] lines)
    {
        foreach (var line in lines)
        {
            events.Read(line);
        }
    }

    [Fact]
    public void Keeps_the_text_of_the_last_agent_message()
    {
        var events = Events();

        ReadAll(events, AgentMessage("I will think."), AgentMessage("{\"skillFile\":\"x\"}"));

        Assert.Equal("{\"skillFile\":\"x\"}", events.LastAgentMessage);
    }

    [Fact]
    public void Has_no_message_before_one_arrives()
    {
        var events = Events();

        ReadAll(events, "{\"type\":\"thread.started\",\"thread_id\":\"abc\"}", "{\"type\":\"turn.started\"}");

        Assert.Null(events.LastAgentMessage);
        Assert.False(events.ToolRan);
    }

    [Fact]
    public void Keeps_the_message_of_a_failed_turn()
    {
        var events = Events();

        events.Read("{\"type\":\"turn.failed\",\"error\":{\"message\":\"You've hit your usage limit.\"}}");

        Assert.Equal("You've hit your usage limit.", events.TurnFailure);
    }

    [Fact]
    public void A_turn_that_completed_has_no_failure()
    {
        var events = Events();

        events.Read("{\"type\":\"turn.completed\",\"usage\":{}}");

        Assert.Null(events.TurnFailure);
    }

    [Theory]
    [InlineData("command_execution", "completed")]
    [InlineData("command_execution", "in_progress")]
    [InlineData("file_change", "completed")]
    [InlineData("mcp_tool_call", "completed")]
    [InlineData("web_search", "completed")]
    [InlineData("a_kind_scion_has_never_seen", "completed")]
    public void A_finished_item_that_is_neither_text_nor_reasoning_nor_an_error_means_a_tool_ran(string type, string status)
    {
        var events = Events();

        events.Read(Completed($"{{\"id\":\"item_2\",\"type\":\"{type}\",\"status\":\"{status}\"}}"));

        Assert.True(events.ToolRan);
        Assert.Equal(1, _toolCallbacks);
    }

    [Fact]
    public void An_unknown_kind_without_a_status_means_a_tool_ran()
    {
        var events = Events();

        events.Read(Completed("{\"id\":\"item_2\",\"type\":\"a_kind_scion_has_never_seen\"}"));

        Assert.True(events.ToolRan);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("declined")]
    public void A_tool_that_failed_or_was_declined_did_not_run(string status)
    {
        var events = Events();

        events.Read(Completed($"{{\"id\":\"item_2\",\"type\":\"command_execution\",\"status\":\"{status}\"}}"));

        Assert.False(events.ToolRan);
        Assert.Equal(0, _toolCallbacks);
    }

    [Fact]
    public void The_warning_that_code_mode_is_unavailable_is_an_error_item_and_not_a_tool()
    {
        var events = Events();

        events.Read(Completed(
            "{\"id\":\"item_0\",\"type\":\"error\",\"message\":\"Code Mode is unavailable because code-mode host is disabled.\"}"));

        Assert.False(events.ToolRan);
    }

    [Fact]
    public void Reasoning_and_messages_are_not_tools()
    {
        var events = Events();

        ReadAll(events, Completed("{\"id\":\"item_0\",\"type\":\"reasoning\",\"text\":\"hmm\"}"), AgentMessage("text"));

        Assert.False(events.ToolRan);
    }

    [Fact]
    public void The_reconnecting_lines_at_the_top_level_are_not_tools()
    {
        var events = Events();

        events.Read("{\"type\":\"error\",\"message\":\"Reconnecting... 2/5 (unexpected status 401)\"}");

        Assert.False(events.ToolRan);
        Assert.Null(events.TurnFailure);
    }

    [Fact]
    public void Tells_the_caller_once_however_many_tools_ran()
    {
        var events = Events();

        ReadAll(
            events,
            Completed("{\"id\":\"item_2\",\"type\":\"command_execution\",\"status\":\"completed\"}"),
            Completed("{\"id\":\"item_3\",\"type\":\"file_change\",\"status\":\"completed\"}"));

        Assert.Equal(1, _toolCallbacks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"type\":\"item.completed\"}")]
    [InlineData("{\"type\":\"item.completed\",\"item\":42}")]
    [InlineData("{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\"}}")]
    public void Lines_that_are_not_events_are_ignored(string line)
    {
        var events = Events();

        events.Read(line);

        Assert.Null(events.LastAgentMessage);
        Assert.False(events.ToolRan);
    }
}
