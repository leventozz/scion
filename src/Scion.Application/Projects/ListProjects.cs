using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.Application.Projects;

public sealed class ListProjects(IProjectRegistry registry, ListSkills listSkills, IFolderProbe folderProbe)
{
    public Task<Result<ProjectOverview, RegistryFailure>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        RegistryGuard.RunAsync(() => OverviewAsync(cancellationToken));

    private async Task<ProjectOverview> OverviewAsync(CancellationToken cancellationToken)
    {
        var personal = new Location.Personal();
        var personalSummary = new PersonalSummary(
            await CountAsync(personal, Agent.ClaudeCode, cancellationToken),
            await CountAsync(personal, Agent.Codex, cancellationToken));

        var registered = await registry.ListAsync(cancellationToken);
        var projects = new List<ProjectSummary>();

        foreach (var project in registered
                     .OrderByDescending(project => project.LastOpenedAt)
                     .ThenBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            projects.Add(await SummarizeAsync(project, cancellationToken));
        }

        return new ProjectOverview(personalSummary, projects);
    }

    private async Task<ProjectSummary> SummarizeAsync(RegisteredProject project, CancellationToken cancellationToken)
    {
        if (!folderProbe.Exists(project.Path))
        {
            return new ProjectSummary(project.Path, project.DisplayName, 0, 0, FolderMissing: true);
        }

        var location = new Location.Project(project.Path);

        return new ProjectSummary(
            project.Path,
            project.DisplayName,
            await CountAsync(location, Agent.ClaudeCode, cancellationToken),
            await CountAsync(location, Agent.Codex, cancellationToken));
    }

    private async Task<int> CountAsync(Location location, Agent agent, CancellationToken cancellationToken) =>
        (await listSkills.ExecuteAsync(location, agent, cancellationToken)).Count;
}
