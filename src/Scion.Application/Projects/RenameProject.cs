using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class RenameProject(IProjectRegistry registry)
{
    public Task<Result<RenamedProject, RenameProjectError>> ExecuteAsync(
        string path, string displayName, CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(
            () => RenameAsync(path, displayName, cancellationToken),
            failure => new RenameProjectError.Registry(failure));

    private async Task<Result<RenamedProject, RenameProjectError>> RenameAsync(
        string path, string displayName, CancellationToken cancellationToken)
    {
        var project = await registry.FindByPathAsync(path, cancellationToken);

        if (project is null)
        {
            return Result<RenamedProject, RenameProjectError>.Failure(new RenameProjectError.NotRegistered());
        }

        var renamed = project.Rename(displayName);

        if (!renamed.IsSuccess)
        {
            return Result<RenamedProject, RenameProjectError>.Failure(new RenameProjectError.NameEmpty());
        }

        if (renamed.Value != project)
        {
            await registry.SaveAsync(renamed.Value, cancellationToken);
        }

        return Result<RenamedProject, RenameProjectError>.Success(
            new RenamedProject(renamed.Value.Path, renamed.Value.DisplayName));
    }
}
