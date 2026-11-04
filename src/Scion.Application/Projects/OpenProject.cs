using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class OpenProject(IProjectRegistry registry, TimeProvider time, IFolderProbe folderProbe)
{
    public Task<Result<OpenedProject, OpenProjectError>> ExecuteAsync(
        string path, CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(
            () => OpenAsync(path, cancellationToken),
            failure => new OpenProjectError.Registry(failure));

    private async Task<Result<OpenedProject, OpenProjectError>> OpenAsync(string path, CancellationToken cancellationToken)
    {
        var project = await registry.FindByPathAsync(path, cancellationToken);

        if (project is null)
        {
            return Result<OpenedProject, OpenProjectError>.Failure(new OpenProjectError.NotRegistered());
        }

        var opened = project.Open(time.GetUtcNow());
        await registry.SaveAsync(opened, cancellationToken);

        return Result<OpenedProject, OpenProjectError>.Success(
            new OpenedProject(opened.Path, opened.DisplayName, FolderMissing: !folderProbe.Exists(opened.Path)));
    }
}
