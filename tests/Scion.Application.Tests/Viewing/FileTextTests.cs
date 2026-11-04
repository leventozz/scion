using System.Text;
using Scion.Application.Viewing;

namespace Scion.Application.Tests.Viewing;

public class FileTextTests
{
    [Fact]
    public void Decodes_utf8_text()
    {
        var text = Assert.IsType<FileText.Shown>(FileText.Decode(Bytes.Of("héllo wörld\n")));

        Assert.Equal("héllo wörld\n", text.Text);
    }

    [Fact]
    public void Strips_a_utf8_byte_order_mark()
    {
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("# Title")).ToArray();

        var text = Assert.IsType<FileText.Shown>(FileText.Decode(new FileBytes(withBom, withBom.Length)));

        Assert.Equal("# Title", text.Text);
    }

    [Fact]
    public void An_empty_file_is_shown_as_empty_text()
    {
        var text = Assert.IsType<FileText.Shown>(FileText.Decode(Bytes.Of("")));

        Assert.Equal("", text.Text);
    }

    [Fact]
    public void A_file_with_a_nul_byte_in_its_first_8_KB_is_not_shown()
    {
        var content = Encoding.UTF8.GetBytes("abc").Concat(new byte[] { 0 }).Concat(Encoding.UTF8.GetBytes("def")).ToArray();

        Assert.IsType<FileText.NotShown>(FileText.Decode(new FileBytes(content, content.Length)));
    }

    [Fact]
    public void A_nul_byte_after_the_first_8_KB_does_not_count_as_binary()
    {
        var content = Enumerable.Repeat((byte)'a', 8192).Concat(new byte[] { 0 }).ToArray();

        // The NUL sits past the sniffed window, and U+0000 is valid UTF-8, so the text is shown.
        Assert.IsType<FileText.Shown>(FileText.Decode(new FileBytes(content, content.Length)));
    }

    [Fact]
    public void A_file_that_is_not_valid_utf8_is_not_shown()
    {
        var latin1 = new byte[] { 0x63, 0x61, 0x66, 0xE9 };

        Assert.IsType<FileText.NotShown>(FileText.Decode(new FileBytes(latin1, latin1.Length)));
    }

    [Fact]
    public void A_file_of_exactly_1_MB_is_shown()
    {
        var content = Enumerable.Repeat((byte)'a', FileText.MaxBytes).ToArray();

        Assert.IsType<FileText.Shown>(FileText.Decode(new FileBytes(content, content.Length)));
    }

    [Fact]
    public void A_file_longer_than_1_MB_is_not_shown()
    {
        var head = Enumerable.Repeat((byte)'a', FileText.MaxBytes + 1).ToArray();

        Assert.IsType<FileText.NotShown>(FileText.Decode(new FileBytes(head, FileText.MaxBytes + 1)));
    }

    [Fact]
    public void The_limit_is_1_048_576_bytes()
    {
        Assert.Equal(1_048_576, FileText.MaxBytes);
    }
}
