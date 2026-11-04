namespace Scion.Application.Projects;

public sealed record ProjectOverview(PersonalSummary Personal, IReadOnlyList<ProjectSummary> Projects);
