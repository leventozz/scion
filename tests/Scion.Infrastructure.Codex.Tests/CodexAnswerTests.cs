namespace Scion.Infrastructure.Codex.Tests;

public class CodexAnswerTests
{
    [Fact]
    public void Takes_the_skill_file_from_the_json_the_agent_answered_with()
    {
        var answer = CodexAnswer.Read("{\"skillFile\":\"---\\nname: docker\\n---\\n# Docker\"}");

        Assert.Equal(new CodexAnswer.SkillFile("---\nname: docker\n---\n# Docker"), answer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_message_is_no_answer(string? message) =>
        Assert.IsType<CodexAnswer.Missing>(CodexAnswer.Read(message));

    [Theory]
    [InlineData("I could not do it.")]
    [InlineData("[\"skillFile\"]")]
    [InlineData("{\"other\":\"x\"}")]
    [InlineData("{\"skillFile\":42}")]
    [InlineData("{\"skillFile\":\"\"}")]
    [InlineData("{\"skillFile\":\"  \"}")]
    public void A_message_without_a_skill_file_is_not_an_answer(string message) =>
        Assert.IsType<CodexAnswer.Unreadable>(CodexAnswer.Read(message));
}
