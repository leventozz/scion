using Scion.Application.Viewing;
using Scion.Domain;

namespace Scion.Application.Editing;

public sealed class OpenFileForEditing(IEditableFileReader reader)
{
    public async Task<Result<EditableText, OpenForEditingError>> ExecuteAsync(
        EditableFile file, CancellationToken cancellationToken = default)
    {
        if (!EditablePaths.IsSafe(file))
        {
            return Result<EditableText, OpenForEditingError>.Failure(OpenForEditingError.NotInSkillFolder);
        }

        var read = await reader.ReadAsync(file, FileText.MaxBytes, cancellationToken);
        if (!read.IsSuccess)
        {
            return Result<EditableText, OpenForEditingError>.Failure(
                read.Error == FileReadError.Missing ? OpenForEditingError.Missing : OpenForEditingError.Unreadable);
        }

        if (FileText.Decode(read.Value) is not FileText.Shown shown)
        {
            return Result<EditableText, OpenForEditingError>.Failure(OpenForEditingError.NotEditable);
        }

        var bytes = read.Value.Head;
        var baseline = new EditBaseline(ContentHash.Of(bytes), TextFormat.Of(bytes));

        return Result<EditableText, OpenForEditingError>.Success(
            new EditableText(LineBreaks.Normalize(shown.Text), baseline));
    }
}
