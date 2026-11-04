using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class RegisterProject(IProjectRegistry registry, TimeProvider time)
{
    private static readonly char[] Separators = ['/', '\\'];

    public Task<Result<OpenedProject, RegistryFailure>> ExecuteAsync(
        string path, CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(() => RegisterAsync(path, cancellationToken));

    private async Task<OpenedProject> RegisterAsync(string path, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var existing = await registry.FindByPathAsync(path, cancellationToken);
        var project = existing?.Open(now) ?? new RegisteredProject(path, DisplayNameOf(path), now);

        await registry.SaveAsync(project, cancellationToken);

        return new OpenedProject(project.Path, project.DisplayName);
    }

    private static string DisplayNameOf(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        var segment = trimmed[(trimmed.LastIndexOfAny(Separators) + 1)..];

        return segment.Length == 0 || segment.EndsWith(':') ? path : segment;
    }
}
