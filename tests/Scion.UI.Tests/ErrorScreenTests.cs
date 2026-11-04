using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Scion.UI.Tests;

public class ErrorScreenTests : BunitContext
{
    public ErrorScreenTests() => Services.AddSingleton(new UserHome("/home/me", StringComparison.Ordinal));

    [Fact]
    public void Shows_the_title_the_path_the_actions_and_the_note()
    {
        var screen = Render<ErrorScreen>(parameters => parameters
            .Add(p => p.Title, "Something failed")
            .Add(p => p.Path, "/home/me/.scion/projects.json")
            .Add(p => p.Note, "Nothing was lost.")
            .Add(p => p.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button>Try again</button>"))));

        Assert.Equal("Something failed", screen.Find("h1").TextContent);
        Assert.Equal("~/.scion/projects.json", screen.Find(".error-path").TextContent);
        Assert.Equal("Try again", screen.Find(".error-actions button").TextContent);
        Assert.Equal("Nothing was lost.", screen.Find(".error-note").TextContent);
    }

    [Fact]
    public void Leaves_out_the_path_and_the_note_when_there_are_none()
    {
        var screen = Render<ErrorScreen>(parameters => parameters.Add(p => p.Title, "Something failed"));

        Assert.Empty(screen.FindAll(".error-path"));
        Assert.Empty(screen.FindAll(".error-note"));
    }
}
