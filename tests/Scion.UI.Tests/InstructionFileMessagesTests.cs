using Scion.Domain;

namespace Scion.UI.Tests;

public class InstructionFileMessagesTests
{
    [Theory]
    [InlineData(Agent.ClaudeCode, "CLAUDE.md")]
    [InlineData(Agent.Codex, "AGENTS.md")]
    public void Each_agent_names_its_instruction_file(Agent agent, string expected)
    {
        Assert.Equal(expected, AgentLabels.InstructionFileName(agent));
    }

    [Theory]
    [InlineData("CLAUDE.md", "./CLAUDE.md")]
    [InlineData(".claude/CLAUDE.md", "./.claude/CLAUDE.md")]
    public void A_project_file_is_shown_relative_to_the_project(string relativePath, string expected)
    {
        Assert.Equal(expected, InstructionFileMessages.PathLabel(new Location.Project("/work/api"), relativePath));
    }

    [Fact]
    public void A_personal_file_is_shown_relative_to_the_home_directory()
    {
        Assert.Equal("~/.codex/AGENTS.md", InstructionFileMessages.PathLabel(new Location.Personal(), ".codex/AGENTS.md"));
    }

    [Theory]
    [InlineData(false, Agent.ClaudeCode, "No CLAUDE.md in this project")]
    [InlineData(false, Agent.Codex, "No AGENTS.md in this project")]
    [InlineData(true, Agent.ClaudeCode, "No personal CLAUDE.md")]
    [InlineData(true, Agent.Codex, "No personal AGENTS.md")]
    public void The_empty_text_speaks_of_the_project_or_of_personal(bool personal, Agent agent, string expected)
    {
        Location location = personal ? new Location.Personal() : new Location.Project("/work/api");

        Assert.Equal(expected, InstructionFileMessages.Empty(location, agent));
    }
}
