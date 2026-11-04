using Scion.Domain;

namespace Scion.Application.Viewing;

public sealed class ListSkillFiles(ISkillFiles files)
{
    public async Task<Result<SkillFileTree, SkillViewError>> ExecuteAsync(
        Location location, Agent agent, string folderName, CancellationToken cancellationToken = default)
    {
        SkillFolderListing? listing;
        try
        {
            listing = await files.ListAsync(location, agent, folderName, cancellationToken);
        }
        catch (FileViewFailedException)
        {
            return Result<SkillFileTree, SkillViewError>.Failure(SkillViewError.Unreadable);
        }

        return listing is null
            ? Result<SkillFileTree, SkillViewError>.Failure(SkillViewError.SkillFolderMissing)
            : Result<SkillFileTree, SkillViewError>.Success(
                new SkillFileTree(listing.FolderPath, SkillFileTreeBuilder.Build(listing.FilePaths)));
    }
}
