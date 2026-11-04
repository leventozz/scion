using Bunit;
using Microsoft.JSInterop;

namespace Scion.UI.Tests;

public class ClampedTextTests : BunitContext
{
    public ClampedTextTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Shows_the_text_clamped_and_without_Show_more_until_the_browser_reports_an_overflow()
    {
        var text = RenderText("A long description.");

        Assert.Equal("A long description.", text.Find(".clamped-text-body").TextContent);
        Assert.Contains("clamped", text.Find(".clamped-text-body").ClassList);
        Assert.Empty(text.FindAll(".text-action"));
    }

    [Fact]
    public async Task Starts_observing_its_own_element_after_the_first_render()
    {
        RenderText("Text");

        var invocation = Assert.Single(JSInterop.Invocations["scion.observeClamp"]);
        Assert.IsType<DotNetObjectReference<ClampedText>>(invocation.Arguments[1]);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Offers_Show_more_when_the_text_overflows_and_expands_it_on_click()
    {
        var text = RenderText("A very long description.");

        await ReportOverflowAsync(text, true);
        text.Find(".text-action").Click();

        Assert.DoesNotContain("clamped", text.Find(".clamped-text-body").ClassList);
        Assert.Equal("Show less", text.Find(".text-action").TextContent);
    }

    [Fact]
    public async Task Show_less_clamps_the_text_again()
    {
        var text = RenderText("A very long description.");
        await ReportOverflowAsync(text, true);
        text.Find(".text-action").Click();

        text.Find(".text-action").Click();

        Assert.Contains("clamped", text.Find(".clamped-text-body").ClassList);
        Assert.Equal("Show more", text.Find(".text-action").TextContent);
    }

    [Fact]
    public async Task Keeps_Show_less_while_expanded_even_though_an_expanded_text_never_overflows()
    {
        var text = RenderText("A very long description.");
        await ReportOverflowAsync(text, true);
        text.Find(".text-action").Click();

        await ReportOverflowAsync(text, false);

        Assert.Equal("Show less", text.Find(".text-action").TextContent);
    }

    [Fact]
    public async Task Removes_Show_more_when_the_text_no_longer_overflows()
    {
        var text = RenderText("A description.");
        await ReportOverflowAsync(text, true);

        await ReportOverflowAsync(text, false);

        Assert.Empty(text.FindAll(".text-action"));
    }

    [Fact]
    public async Task Stops_observing_when_it_goes_away()
    {
        var text = RenderText("Text");

        await text.Instance.DisposeAsync();

        Assert.Single(JSInterop.Invocations["scion.unobserveClamp"]);
    }

    private IRenderedComponent<ClampedText> RenderText(string content) =>
        Render<ClampedText>(parameters => parameters.Add(text => text.Text, content));

    private static async Task ReportOverflowAsync(IRenderedComponent<ClampedText> text, bool overflowing) =>
        await text.InvokeAsync(() => text.Instance.SetOverflowing(overflowing));
}
