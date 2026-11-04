using System.Text;
using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Tests.Viewing;

internal sealed class InMemoryInstructionFiles : IInstructionFiles
{
    private readonly Dictionary<(Location, Agent), IReadOnlyList<InstructionFileBytes>> _files = new();

    public int? LastMaxBytes { get; private set; }

    public void Add(Location location, Agent agent, params InstructionFileBytes[] files) => _files[(location, agent)] = files;

    public static InstructionFileBytes File(string relativePath, string folderPath, string content) =>
        new(relativePath, folderPath, Bytes.Read(Encoding.UTF8.GetBytes(content)));

    public Task<IReadOnlyList<InstructionFileBytes>> ReadAsync(
        Location location, Agent agent, int maxBytes, CancellationToken cancellationToken)
    {
        LastMaxBytes = maxBytes;

        return Task.FromResult(_files.TryGetValue((location, agent), out var files) ? files : []);
    }
}
