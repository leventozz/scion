using Bunit;

namespace Scion.UI.Tests;

public class StreamingTextTests : BunitContext
{
    public StreamingTextTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<StreamingText> Render(string text) =>
        Render<StreamingText>(parameters => parameters.Add(box => box.Text, text));

    [Fact]
    public void Before_the_first_text_it_shows_only_the_sparkle_in_the_middle_of_the_box()
    {
        var box = Render("");

        var text = box.Find(".streaming-text");
        Assert.Contains("streaming-empty", text.ClassList);
        Assert.Equal("", text.Own());
        Assert.Single(box.FindAll(".sparkle"));
        Assert.Empty(box.FindAll(".streaming-cursor"));
    }

    [Fact]
    public void The_sparkle_has_the_six_frames_in_order_and_is_hidden_from_assistive_technology()
    {
        var box = Render("");

        var sparkle = box.Find(".sparkle");
        Assert.Equal("true", sparkle.GetAttribute("aria-hidden"));
        Assert.Equal(
            Enumerable.Range(0, 6).Select(index => $"sparkle-frame-{index}"),
            sparkle.QuerySelectorAll(".sparkle-frame").Select(frame => frame.ClassList.Single(name => name != "sparkle-frame")));
    }

    [Fact]
    public void The_shapes_are_drawn_not_typed_and_carry_no_text_logo_or_name()
    {
        var sparkle = Render("").Find(".sparkle");

        Assert.Equal("", sparkle.TextContent.Trim());
        Assert.Empty(sparkle.QuerySelectorAll("text, image, img, title, desc"));
        Assert.NotEmpty(sparkle.QuerySelectorAll("circle, ellipse, line, polygon"));
    }

    [Fact]
    public void Once_text_has_arrived_the_sparkle_is_the_cursor_at_the_end_of_it()
    {
        var box = Render("---\nname: docker");

        var text = box.Find(".streaming-text");
        Assert.DoesNotContain("streaming-empty", text.ClassList);
        Assert.Equal("---\nname: docker", text.Own());
        Assert.Single(box.FindAll(".sparkle"));
        Assert.Equal("streaming-cursor", text.LastElementChild!.ClassName);
        Assert.NotNull(box.Find(".streaming-cursor .sparkle"));
    }

    [Fact]
    public void The_sparkle_moves_from_the_middle_to_the_end_when_the_first_text_arrives()
    {
        var box = Render("");

        box.Render(parameters => parameters.Add(component => component.Text, "first"));

        Assert.Empty(box.FindAll(".streaming-empty"));
        Assert.Single(box.FindAll(".streaming-cursor"));
        Assert.Single(box.FindAll(".sparkle"));
    }
}
