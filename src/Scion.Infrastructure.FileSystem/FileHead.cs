using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Infrastructure.FileSystem;

internal static class FileHead
{
    /// <summary>
    /// At most <paramref name="maxBytes"/> + 1 bytes of the file, so a huge file is never loaded, with its real
    /// length. Another process may keep the file open for writing. A failed result when it cannot be read:
    /// <see cref="FileReadError.Missing"/> when the file or its folder does not exist, otherwise
    /// <see cref="FileReadError.Unreadable"/>.
    /// </summary>
    public static async Task<Result<FileBytes, FileReadError>> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);

            var length = stream.Length;
            var head = new byte[Math.Min(length, maxBytes + 1L)];
            var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);

            return Result<FileBytes, FileReadError>.Success(new FileBytes(head[..read], length));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Result<FileBytes, FileReadError>.Failure(FileReadError.Missing);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<FileBytes, FileReadError>.Failure(FileReadError.Unreadable);
        }
    }
}
