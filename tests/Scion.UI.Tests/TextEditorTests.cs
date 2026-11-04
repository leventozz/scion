using Bunit;

namespace Scion.UI.Tests;

public class TextEditorTests : BunitContext
{
    [Fact]
    public void Is_a_plain_text_area_without_spell_checking_or_wrapping_and_with_a_label()
    {
        var editor = RenderEditor("hello");

        var area = editor.Find("textarea");
        Assert.Equal("off", area.GetAttribute("wrap"));
        Assert.Equal("false", area.GetAttribute("spellcheck"));
        Assert.Equal("SKILL.md", area.GetAttribute("aria-label"));
        Assert.Equal("hello", area.GetAttribute("value"));
    }

    [Theory]
    [InlineData("", "1")]
    [InlineData("one", "1")]
    [InlineData("a\nb\nc", "1\n2\n3")]
    [InlineData("a\n", "1\n2")]
    [InlineData("a\n\n\nb", "1\n2\n3\n4")]
    public void Numbers_every_line_in_the_gutter_including_the_empty_line_after_a_final_line_break(string text, string numbers)
    {
        var editor = RenderEditor(text);

        Assert.Equal(numbers, editor.Find(".text-editor-gutter").TextContent);
    }

    [Fact]
    public void Hides_the_gutter_from_assistive_technology()
    {
        Assert.Equal("true", RenderEditor("a").Find(".text-editor-gutter").GetAttribute("aria-hidden"));
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("ab\nabcd", 2, 5)]
    [InlineData("one line\n", 2, 9)]
    public void Sizes_the_text_area_to_its_text_so_the_editor_scrolls_as_one(string text, int rows, int cols)
    {
        var area = RenderEditor(text).Find("textarea");

        Assert.Equal(rows.ToString(), area.GetAttribute("rows"));
        Assert.Equal(cols.ToString(), area.GetAttribute("cols"));
    }

    [Fact]
    public void Reports_every_input_with_the_new_text()
    {
        string? reported = null;
        var editor = RenderEditor("old", text => reported = text);

        editor.Find("textarea").Input("new\ntext");

        Assert.Equal("new\ntext", reported);
    }

    [Fact]
    public void Does_not_write_what_was_typed_back_into_the_text_area_when_the_app_hands_it_on_again()
    {
        var editor = RenderEditor("");

        editor.Find("textarea").Input("abc");
        editor.Render(parameters => parameters.Add(component => component.Text, "abc"));

        Assert.Equal("", editor.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Writes_a_text_that_changed_from_outside_even_after_typing()
    {
        var editor = RenderEditor("");
        editor.Find("textarea").Input("abc");
        editor.Render(parameters => parameters.Add(component => component.Text, "abc"));

        editor.Render(parameters => parameters.Add(component => component.Text, "reloaded"));

        Assert.Equal("reloaded", editor.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Still_measures_the_text_it_is_given_while_the_text_area_is_left_alone()
    {
        var editor = RenderEditor("a");

        editor.Find("textarea").Input("a\nb\nc");
        editor.Render(parameters => parameters.Add(component => component.Text, "a\nb\nc"));

        Assert.Equal("1\n2\n3", editor.Find(".text-editor-gutter").TextContent);
        Assert.Equal("3", editor.Find("textarea").GetAttribute("rows"));
    }

    private IRenderedComponent<TextEditor> RenderEditor(string text, Action<string>? onChanged = null) =>
        Render<TextEditor>(parameters => parameters
            .Add(editor => editor.Text, text)
            .Add(editor => editor.Label, "SKILL.md")
            .Add(editor => editor.TextChanged, onChanged ?? (_ => { })));
}
