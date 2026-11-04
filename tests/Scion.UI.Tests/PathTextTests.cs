using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Scion.UI.Tests;

public class PathTextTests : BunitContext
{
    public PathTextTests() => Services.AddSingleton(new UserHome("/home/me", StringComparison.Ordinal));

    [Fact]
    public void Shows_the_path_with_the_home_directory_abbreviated_and_the_last_folder_apart()
    {
        var path = Render<PathText>(parameters => parameters.Add(p => p.Path, "/home/me/work/api"));

        Assert.Equal("~/work/", path.Find(".path-head").TextContent);
        Assert.Equal("api", path.Find(".path-tail").TextContent);
        Assert.Equal("~/work/api", path.Find(".path").TextContent);
    }

    [Fact]
    public void Leaves_a_path_outside_the_home_directory_alone()
    {
        var path = Render<PathText>(parameters => parameters.Add(p => p.Path, "/srv/apps/api"));

        Assert.Equal("/srv/apps/api", path.Find(".path").TextContent);
    }
}
