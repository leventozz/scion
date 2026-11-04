namespace Scion.Infrastructure.ClaudeCode.Tests;

public class SkillFileJsonDecoderTests
{
    private static string DecodeInOnePiece(string json) => new SkillFileJsonDecoder().Append(json);

    private static string DecodeSplitAt(string json, int index)
    {
        var decoder = new SkillFileJsonDecoder();

        return decoder.Append(json[..index]) + decoder.Append(json[index..]);
    }

    private static string DecodeCharByChar(string json)
    {
        var decoder = new SkillFileJsonDecoder();

        return string.Concat(json.Select(character => decoder.Append(character.ToString())));
    }

    public static TheoryData<string, string> Documents => new()
    {
        { "{\"skillFile\":\"abc\"}", "abc" },
        { "{\"skillFile\":\"line one\\nline two\\ttab\"}", "line one\nline two\ttab" },
        { "{\"skillFile\":\"say \\\"hi\\\" \\\\ and \\/ done\"}", "say \"hi\" \\ and / done" },
        { "{\"skillFile\":\"\\u00fcn\\u00EFcode \\u2014 dash\"}", "ünïcode — dash" },
        { "{\"skillFile\":\"\\ud83d\\ude00 smile\"}", "\U0001F600 smile" },
        { "{ \"skillFile\" :\n  \"spaced\" }", "spaced" },
        { "{\"skillFile\":\"done\"} trailing {\"skillFile\":\"ignored\"}", "done" },
        { "{\"skillFile\":\"\"}", "" },
        { "{\"other\":\"not the skill\"}", "" },
        { "{\"skillFile\":42}", "" },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void Decodes_the_skill_file_of_a_complete_document(string json, string expected) =>
        Assert.Equal(expected, DecodeInOnePiece(json));

    [Theory]
    [MemberData(nameof(Documents))]
    public void Gives_the_same_text_wherever_the_document_is_split(string json, string expected)
    {
        for (var index = 0; index <= json.Length; index++)
        {
            Assert.Equal(expected, DecodeSplitAt(json, index));
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Gives_the_same_text_one_character_at_a_time(string json, string expected) =>
        Assert.Equal(expected, DecodeCharByChar(json));

    [Fact]
    public void Gives_each_piece_only_the_text_that_is_new()
    {
        var decoder = new SkillFileJsonDecoder();

        Assert.Equal("", decoder.Append("{\"skillFile\":\""));
        Assert.Equal("Hel", decoder.Append("Hel"));
        Assert.Equal("lo\n", decoder.Append("lo\\n"));
        Assert.Equal("", decoder.Append("\"}"));
    }

    [Fact]
    public void Holds_back_a_high_surrogate_until_its_pair_has_arrived()
    {
        var decoder = new SkillFileJsonDecoder();

        Assert.Equal("a", decoder.Append("{\"skillFile\":\"a\\ud83d"));
        Assert.Equal("\U0001F600", decoder.Append("\\ude00"));
    }

    [Fact]
    public void An_unfinished_escape_gives_nothing_yet()
    {
        var decoder = new SkillFileJsonDecoder();

        Assert.Equal("x", decoder.Append("{\"skillFile\":\"x\\u00"));
        Assert.Equal("é", decoder.Append("e9"));
    }
}
