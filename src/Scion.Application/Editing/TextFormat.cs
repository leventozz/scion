using System.Text;

namespace Scion.Application.Editing;

/// <summary>
/// How a text file is laid out on disk, so that an edit writes it back the way it was: with or without a byte order mark,
/// and with the line ending of its first line break.
/// </summary>
public sealed record TextFormat(bool HasBom, LineEnding LineEnding)
{
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    internal static TextFormat Of(byte[] bytes)
    {
        var content = bytes.AsSpan();
        var hasBom = content.StartsWith(ByteOrderMark);

        return new TextFormat(hasBom, LineEndingOf(content));
    }

    internal byte[] Encode(string text)
    {
        var normalized = LineBreaks.Normalize(text);
        var withEnding = LineEnding == LineEnding.CrLf ? normalized.Replace("\n", "\r\n") : normalized;
        var body = Utf8.GetBytes(withEnding);

        return HasBom ? [.. ByteOrderMark, .. body] : body;
    }

    private static LineEnding LineEndingOf(ReadOnlySpan<byte> content)
    {
        var firstBreak = content.IndexOfAny((byte)'\r', (byte)'\n');
        var isCrLf = firstBreak >= 0
            && content[firstBreak] == (byte)'\r'
            && firstBreak + 1 < content.Length
            && content[firstBreak + 1] == (byte)'\n';

        return isCrLf ? LineEnding.CrLf : LineEnding.Lf;
    }
}
