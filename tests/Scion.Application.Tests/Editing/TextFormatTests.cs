using System.Text;
using Scion.Application.Editing;

namespace Scion.Application.Tests.Editing;

public class TextFormatTests
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void A_plain_LF_file_has_no_BOM_and_LF_endings() =>
        Assert.Equal(new TextFormat(false, LineEnding.Lf), TextFormat.Of(Utf8("a\nb\n")));

    [Fact]
    public void Detects_a_BOM() =>
        Assert.True(TextFormat.Of([.. Bom, .. Utf8("a\n")]).HasBom);

    [Fact]
    public void Detects_CRLF_from_the_first_line_break() =>
        Assert.Equal(LineEnding.CrLf, TextFormat.Of(Utf8("a\r\nb\r\n")).LineEnding);

    [Fact]
    public void The_first_line_break_wins_over_later_ones() =>
        Assert.Equal(LineEnding.Lf, TextFormat.Of(Utf8("a\nb\r\nc\r\n")).LineEnding);

    [Fact]
    public void A_file_without_a_line_break_gets_LF() =>
        Assert.Equal(LineEnding.Lf, TextFormat.Of(Utf8("single line")).LineEnding);

    [Fact]
    public void A_lone_CR_counts_as_LF() =>
        Assert.Equal(LineEnding.Lf, TextFormat.Of(Utf8("a\rb\r\nc")).LineEnding);

    [Fact]
    public void Encodes_with_the_LF_ending() =>
        Assert.Equal(Utf8("a\nb"), new TextFormat(false, LineEnding.Lf).Encode("a\nb"));

    [Fact]
    public void Encodes_with_the_CRLF_ending() =>
        Assert.Equal(Utf8("a\r\nb\r\n"), new TextFormat(false, LineEnding.CrLf).Encode("a\nb\n"));

    [Fact]
    public void Never_doubles_a_carriage_return_when_the_text_already_has_CRLF() =>
        Assert.Equal(Utf8("a\r\nb"), new TextFormat(false, LineEnding.CrLf).Encode("a\r\nb"));

    [Fact]
    public void Encodes_mixed_endings_into_the_chosen_style() =>
        Assert.Equal(Utf8("a\nb\nc"), new TextFormat(false, LineEnding.Lf).Encode("a\r\nb\rc"));

    [Fact]
    public void Writes_the_BOM_first_when_the_file_had_one() =>
        Assert.Equal([.. Bom, .. Utf8("a")], new TextFormat(true, LineEnding.Lf).Encode("a"));

    [Fact]
    public void Writes_no_BOM_when_the_file_had_none() =>
        Assert.Equal(Utf8("a"), new TextFormat(false, LineEnding.Lf).Encode("a"));

    [Fact]
    public void Encodes_non_ASCII_text_as_UTF8() =>
        Assert.Equal(Utf8("çay ☕"), new TextFormat(false, LineEnding.Lf).Encode("çay ☕"));

    [Fact]
    public void Keeps_the_bytes_of_a_BOM_CRLF_file_when_nothing_changed()
    {
        var original = (byte[])[.. Bom, .. Utf8("---\r\nname: x\r\n---\r\nBody\r\n")];

        var format = TextFormat.Of(original);
        var text = LineBreaks.Normalize(Encoding.UTF8.GetString(original, 3, original.Length - 3));

        Assert.Equal(original, format.Encode(text));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
