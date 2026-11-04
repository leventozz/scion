using Bunit;

namespace Scion.UI.Tests;

public class EditPreviewSwitchTests : BunitContext
{
    [Fact]
    public void Shows_Edit_and_Preview_with_the_current_mode_pressed()
    {
        var edit = RenderSwitch(EditMode.Edit);

        Assert.Equal(
            [("Edit", "true"), ("Preview", "false")],
            edit.FindAll("button").Select(button => (button.TextContent, button.GetAttribute("aria-pressed")!)));
    }

    [Fact]
    public void Presses_Preview_when_that_is_the_mode()
    {
        var preview = RenderSwitch(EditMode.Preview);

        Assert.Equal(
            ["false", "true"],
            preview.FindAll("button").Select(button => button.GetAttribute("aria-pressed")));
    }

    [Fact]
    public void Reports_the_mode_the_user_chooses()
    {
        EditMode? chosen = null;
        var edit = RenderSwitch(EditMode.Edit, mode => chosen = mode);

        edit.FindAll("button")[1].Click();

        Assert.Equal(EditMode.Preview, chosen);
    }

    [Fact]
    public void Is_a_group_with_a_label_and_its_own_style_apart_from_the_agent_switch()
    {
        var group = RenderSwitch(EditMode.Edit).Find(".edit-switch");

        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("View", group.GetAttribute("aria-label"));
        Assert.Empty(RenderSwitch(EditMode.Edit).FindAll(".agent-switch"));
    }

    private IRenderedComponent<EditPreviewSwitch> RenderSwitch(EditMode mode, Action<EditMode>? onChanged = null) =>
        Render<EditPreviewSwitch>(parameters => parameters
            .Add(editSwitch => editSwitch.Mode, mode)
            .Add(editSwitch => editSwitch.ModeChanged, onChanged ?? (_ => { })));
}
