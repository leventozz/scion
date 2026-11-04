using Bunit;

namespace Scion.UI.Tests;

public class ProjectAvatarTests : BunitContext
{
    [Fact]
    public void Shows_the_initials_of_the_project_name()
    {
        var avatar = Render<ProjectAvatar>(parameters => parameters.Add(p => p.Name, "order-payment-api"));

        Assert.Equal("OP", avatar.Find(".avatar").TextContent);
    }

    [Fact]
    public void Takes_its_colors_from_the_tone_class_of_the_name_and_sets_no_inline_style()
    {
        var avatar = Render<ProjectAvatar>(parameters => parameters.Add(p => p.Name, "scion"));

        var element = avatar.Find(".avatar");
        Assert.Contains("avatar-lavender", element.ClassList);
        Assert.Null(element.GetAttribute("style"));
    }

    [Fact]
    public void Gives_equal_names_the_same_color()
    {
        var first = Render<ProjectAvatar>(parameters => parameters.Add(p => p.Name, "scion"));
        var second = Render<ProjectAvatar>(parameters => parameters.Add(p => p.Name, "scion"));

        Assert.Equal(first.Find(".avatar").ClassName, second.Find(".avatar").ClassName);
    }
}
