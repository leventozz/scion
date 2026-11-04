using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.Application.Tests.Skills.Copying;

public class PathComparisonTests
{
    private static readonly PathComparison IgnoringCase = new(StringComparison.OrdinalIgnoreCase);
    private static readonly PathComparison RespectingCase = new(StringComparison.Ordinal);

    [Fact]
    public void Two_personal_locations_are_the_same()
    {
        Assert.True(RespectingCase.Same(new Location.Personal(), new Location.Personal()));
    }

    [Fact]
    public void Personal_and_a_project_are_never_the_same()
    {
        Assert.False(IgnoringCase.Same(new Location.Personal(), new Location.Project("/work/api")));
        Assert.False(IgnoringCase.Same(new Location.Project("/work/api"), new Location.Personal()));
    }

    [Fact]
    public void Projects_are_the_same_when_their_paths_match_under_the_comparison()
    {
        var upper = new Location.Project("/work/API");
        var lower = new Location.Project("/work/api");

        Assert.True(IgnoringCase.Same(upper, lower));
        Assert.False(RespectingCase.Same(upper, lower));
    }

    [Fact]
    public void Folder_names_are_compared_with_the_same_rule()
    {
        Assert.True(IgnoringCase.Same("Docker", "docker"));
        Assert.False(RespectingCase.Same("Docker", "docker"));
    }
}
