using Scion.Application.Creating;
using Scion.Application.Skills.Copying;
using Scion.Application.Tests.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Creating;

public class PrepareNewSkillTests
{
    private static readonly AgentLocation Api = new(new Location.Project("/work/api"), Agent.ClaudeCode);

    private readonly InMemorySkillFolders _folders = new();

    private PrepareNewSkill Prepare() => new(_folders, new PathComparison(StringComparison.Ordinal));

    [Fact]
    public async Task The_form_knows_the_skill_folders_that_are_already_there()
    {
        _folders.Add(Api, "docker");

        var form = await Prepare().ExecuteAsync(Api);

        Assert.Equal(NewSkillProblem.NameTaken, form.Check("docker", "Runs containers.").NameProblem);
        Assert.True(form.Check("testing", "Runs tests.").CanCreate);
    }

    [Fact]
    public async Task Another_agent_or_location_has_its_own_names()
    {
        _folders.Add(Api, "docker");

        var form = await Prepare().ExecuteAsync(Api with { Agent = Agent.Codex });

        Assert.True(form.Check("docker", "Runs containers.").CanCreate);
    }
}
