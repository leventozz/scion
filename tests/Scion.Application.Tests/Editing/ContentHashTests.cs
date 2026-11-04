using System.Text;
using Scion.Application.Editing;

namespace Scion.Application.Tests.Editing;

public class ContentHashTests
{
    [Fact]
    public void Is_the_lowercase_hex_SHA_256_of_the_bytes() =>
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            ContentHash.Of(Encoding.ASCII.GetBytes("abc")));

    [Fact]
    public void Is_the_same_for_the_same_bytes() =>
        Assert.Equal(ContentHash.Of([1, 2, 3]), ContentHash.Of([1, 2, 3]));

    [Fact]
    public void Differs_when_a_single_byte_differs() =>
        Assert.NotEqual(ContentHash.Of([1, 2, 3]), ContentHash.Of([1, 2, 4]));

    [Fact]
    public async Task Hashes_a_stream_to_the_same_value_as_its_bytes()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        await using var stream = new MemoryStream(bytes);

        Assert.Equal(ContentHash.Of(bytes), await ContentHash.OfAsync(stream, CancellationToken.None));
    }
}
