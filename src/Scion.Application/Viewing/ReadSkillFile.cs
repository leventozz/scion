using Scion.Domain;

namespace Scion.Application.Viewing;

public sealed class ReadSkillFile(ISkillFiles files)
{
    private const string SkillFileName = "SKILL.md";

    public async Task<Result<SkillFileView, SkillViewError>> ExecuteAsync(
        Location location, Agent agent, string folderName, string relativePath, CancellationToken cancellationToken = default)
    {
        if (!RelativeFilePath.IsSafe(relativePath))
        {
            return Result<SkillFileView, SkillViewError>.Failure(SkillViewError.NotInSkillFolder);
        }

        var content = await files.ReadAsync(location, agent, folderName, relativePath, FileText.MaxBytes, cancellationToken);

        return Result<SkillFileView, SkillViewError>.Success(ViewOf(content, relativePath));
    }

    private static SkillFileView ViewOf(SkillFileContent content, string relativePath)
    {
        if (!content.Read.IsSuccess || FileText.Decode(content.Read.Value) is not FileText.Shown shown)
        {
            return new SkillFileView.NotShown(content.FolderPath);
        }

        return relativePath == SkillFileName
            ? new SkillFileView.Markdown(SkillMarkdown.BodyOf(shown.Text))
            : new SkillFileView.PlainText(TextLines.Of(shown.Text));
    }
}
