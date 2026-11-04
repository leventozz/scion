using System.Text;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

internal static class Bytes
{
    public static FileBytes Of(string text) => Of(Encoding.UTF8.GetBytes(text));

    /// <summary>What a port returns for <paramref name="content"/>: at most <c>maxBytes + 1</c> bytes and the real length.</summary>
    public static FileBytes Of(byte[] content, int maxBytes = FileText.MaxBytes) =>
        new(content.Take(maxBytes + 1).ToArray(), content.Length);

    public static Result<FileBytes, FileReadError> Read(byte[] content, int maxBytes = FileText.MaxBytes) =>
        Result<FileBytes, FileReadError>.Success(Of(content, maxBytes));

    public static Result<FileBytes, FileReadError> Unreadable =>
        Result<FileBytes, FileReadError>.Failure(FileReadError.Unreadable);
}
