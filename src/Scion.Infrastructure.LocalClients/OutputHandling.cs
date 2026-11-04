namespace Scion.Infrastructure.LocalClients;

/// <summary>What is done with the standard output of the program while it runs.</summary>
public abstract class OutputHandling
{
    private protected OutputHandling()
    {
    }

    /// <summary>Everything is collected and handed back in the outcome.</summary>
    public static readonly OutputHandling Keep = new KeepAll();

    /// <summary>Everything is read and thrown away, never kept or decoded.</summary>
    public static readonly OutputHandling Drop = new DropAll();

    /// <summary>Each line is handed to <paramref name="onLine"/> as it is printed; nothing is kept.</summary>
    public static OutputHandling Lines(Action<string> onLine) => new PerLine(onLine);

    /// <summary>Reads the output to its end and returns what is to be kept of it.</summary>
    public abstract Task<string> ReadAsync(StreamReader reader);

    private sealed class KeepAll : OutputHandling
    {
        public override Task<string> ReadAsync(StreamReader reader) => reader.ReadToEndAsync(CancellationToken.None);
    }

    private sealed class DropAll : OutputHandling
    {
        public override async Task<string> ReadAsync(StreamReader reader)
        {
            await reader.BaseStream.CopyToAsync(Stream.Null);

            return "";
        }
    }

    private sealed class PerLine(Action<string> onLine) : OutputHandling
    {
        public override async Task<string> ReadAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                onLine(line);
            }

            return "";
        }
    }
}
