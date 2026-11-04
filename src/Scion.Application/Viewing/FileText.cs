using System.Text;

namespace Scion.Application.Viewing;

/// <summary>The policy that decides whether the bytes of a file are text Scion shows.</summary>
internal abstract record FileText
{
    public const int MaxBytes = 1_048_576;

    private const int BinarySniffBytes = 8192;
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private FileText()
    {
    }

    public sealed record Shown(string Text) : FileText;

    public sealed record NotShown : FileText;

    public static FileText Decode(FileBytes file)
    {
        if (file.Length > MaxBytes || LooksBinary(file.Head))
        {
            return new NotShown();
        }

        var content = file.Head.AsSpan();
        if (content.StartsWith(ByteOrderMark))
        {
            content = content[ByteOrderMark.Length..];
        }

        try
        {
            return new Shown(StrictUtf8.GetString(content));
        }
        catch (DecoderFallbackException)
        {
            return new NotShown();
        }
    }

    private static bool LooksBinary(byte[] head) => head.AsSpan(0, Math.Min(BinarySniffBytes, head.Length)).Contains((byte)0);
}
