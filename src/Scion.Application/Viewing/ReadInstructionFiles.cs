using Scion.Domain;

namespace Scion.Application.Viewing;

public sealed class ReadInstructionFiles(IInstructionFiles files)
{
    public async Task<IReadOnlyList<InstructionFileView>> ExecuteAsync(
        Location location, Agent agent, CancellationToken cancellationToken = default)
    {
        var found = await files.ReadAsync(location, agent, FileText.MaxBytes, cancellationToken);

        return found.Select(file => new InstructionFileView(file.RelativePath, file.FolderPath, ContentOf(file))).ToList();
    }

    private static InstructionFileContent ContentOf(InstructionFileBytes file) =>
        file.Read.IsSuccess && FileText.Decode(file.Read.Value) is FileText.Shown shown
            ? new InstructionFileContent.Markdown(shown.Text)
            : new InstructionFileContent.NotShown();
}
