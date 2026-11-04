using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Domain;

namespace Scion.UI.Tests;

public class CopyPlacePickerTests : BunitContext
{
    private static readonly DateTimeOffset Noon = UiFakes.Noon;

    private readonly UiFakes _fakes = new();
    private readonly List<CopyPlace> _chosen = [];

    public CopyPlacePickerTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", Noon.AddDays(-1)));
        _fakes.Registry.Add(new RegisteredProject("/work/api", "api", Noon));
    }

    [Fact]
    public void Shows_the_name_of_the_selected_place_on_the_button()
    {
        var picker = RenderPicker(new CopyPlace("api", new Location.Project("/work/api")));

        Assert.Equal("api", picker.Find(".place-picker-button .place-name").TextContent);
    }

    [Fact]
    public void Asks_to_choose_a_project_when_nothing_is_selected()
    {
        var picker = RenderPicker(selected: null);

        Assert.Equal("Choose a project", picker.Find(".place-picker-button .place-name").TextContent);
    }

    [Fact]
    public void Is_closed_until_the_button_is_clicked()
    {
        var picker = RenderPicker(selected: null);

        Assert.Empty(picker.FindAll(".place-picker-panel"));
        Assert.Equal("false", picker.Find(".place-picker-button").GetAttribute("aria-expanded"));

        picker.Find(".place-picker-button").Click();

        Assert.NotNull(picker.Find(".place-picker-panel .place-search"));
        Assert.Equal("true", picker.Find(".place-picker-button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Lists_personal_first_then_the_projects_in_registry_order_with_their_paths()
    {
        var picker = OpenPicker();

        var options = picker.FindAll(".place-option");
        Assert.Equal(["Personal", "api", "web"], options.Select(NameOf));
        Assert.NotNull(options[0].QuerySelector(".folder-tile"));
        Assert.NotNull(options[1].QuerySelector(".avatar"));
        Assert.Equal("/work/", options[1].QuerySelector(".path-head")!.TextContent);
        Assert.Equal("api", options[1].QuerySelector(".path-tail")!.TextContent);
        Assert.NotNull(picker.Find(".place-divider"));
    }

    [Fact]
    public void Labels_personal_with_nothing_but_its_name()
    {
        var picker = OpenPicker();

        Assert.Equal("Personal", picker.FindAll(".place-option")[0].TextContent.Trim());
    }

    [Fact]
    public void Leaves_out_projects_whose_folder_is_missing()
    {
        _fakes.Folders.MarkMissing("/work/web");

        var picker = OpenPicker();

        Assert.Equal(["Personal", "api"], picker.FindAll(".place-option").Select(NameOf));
    }

    [Fact]
    public void Filters_the_projects_by_name_ignoring_case()
    {
        var picker = OpenPicker();

        picker.Find(".place-search").Input("AP");

        Assert.Equal(["api"], picker.FindAll(".place-option").Select(NameOf));
        Assert.Empty(picker.FindAll(".place-divider"));
    }

    [Fact]
    public void Keeps_personal_when_the_search_matches_its_name()
    {
        var picker = OpenPicker();

        picker.Find(".place-search").Input("pers");

        Assert.Equal(["Personal"], picker.FindAll(".place-option").Select(NameOf));
    }

    [Fact]
    public void Choosing_a_project_reports_it_and_closes_the_panel()
    {
        var picker = OpenPicker();

        picker.FindAll(".place-option")[2].Click();

        Assert.Equal([new CopyPlace("web", new Location.Project("/work/web"))], _chosen);
        Assert.Empty(picker.FindAll(".place-picker-panel"));
    }

    [Fact]
    public void Choosing_personal_reports_the_personal_place()
    {
        var picker = OpenPicker();

        picker.FindAll(".place-option")[0].Click();

        Assert.Equal([new CopyPlace("Personal", new Location.Personal())], _chosen);
    }

    [Fact]
    public void Escape_closes_only_the_panel()
    {
        var picker = OpenPicker();

        picker.Find(".place-picker-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(picker.FindAll(".place-picker-panel"));
    }

    [Fact]
    public void Offers_only_personal_and_a_note_when_the_project_list_cannot_be_read()
    {
        _fakes.Registry.IsUnreadable = true;

        var picker = OpenPicker();

        Assert.Equal(["Personal"], picker.FindAll(".place-option").Select(NameOf));
        Assert.Equal("Couldn't read your project list", picker.Find(".place-note").TextContent);
    }

    private IRenderedComponent<CopyPlacePicker> OpenPicker()
    {
        var picker = RenderPicker(selected: null);
        picker.Find(".place-picker-button").Click();

        return picker;
    }

    private IRenderedComponent<CopyPlacePicker> RenderPicker(CopyPlace? selected) =>
        Render<CopyPlacePicker>(parameters => parameters
            .Add(p => p.Selected, selected)
            .Add(p => p.OnSelected, place => _chosen.Add(place)));

    private static string NameOf(IElement option) => option.QuerySelector(".place-option-name")!.TextContent;
}
