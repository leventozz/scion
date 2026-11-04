namespace Scion.UI.Tests;

public class FieldValueTests
{
    [Fact]
    public void Starts_by_rendering_the_text_it_is_given()
    {
        var field = new FieldValue("start");

        Assert.Equal("start", field.Rendered);
        Assert.Equal(0, field.Generation);
    }

    [Fact]
    public void What_the_user_types_is_never_written_back_when_the_app_hands_it_on_again()
    {
        var field = new FieldValue("");

        foreach (var typed in new[] { "R", "Re", "Rev", "Revi" })
        {
            field.Typed(typed);
            field.Offered(typed);
        }

        Assert.Equal("", field.Rendered);
        Assert.Equal(0, field.Generation);
    }

    [Fact]
    public void A_text_that_changed_from_outside_is_written()
    {
        var field = new FieldValue("old");

        field.Offered("reloaded");

        Assert.Equal("reloaded", field.Rendered);
        Assert.Equal(0, field.Generation);
    }

    [Fact]
    public void A_text_that_changed_from_outside_after_typing_is_written()
    {
        var field = new FieldValue("");
        field.Typed("abc");
        field.Offered("abc");

        field.Offered("one two");

        Assert.Equal("one two", field.Rendered);
    }

    [Fact]
    public void Offering_the_same_outside_text_again_changes_nothing_more()
    {
        var field = new FieldValue("old");
        field.Offered("reloaded");

        field.Offered("reloaded");
        field.Offered("reloaded");

        Assert.Equal("reloaded", field.Rendered);
        Assert.Equal(0, field.Generation);
    }

    [Fact]
    public void Going_back_to_the_text_that_was_rendered_after_typing_needs_a_new_field_because_the_old_value_would_not_be_sent()
    {
        var field = new FieldValue("original");
        field.Typed("original and more");
        field.Offered("original and more");

        field.Offered("original");

        Assert.Equal("original", field.Rendered);
        Assert.Equal(1, field.Generation);
    }

    [Fact]
    public void Typing_after_an_outside_change_is_again_not_written_back()
    {
        var field = new FieldValue("old");
        field.Offered("reloaded");

        field.Typed("reloaded!");
        field.Offered("reloaded!");

        Assert.Equal("reloaded", field.Rendered);
        Assert.Equal(0, field.Generation);
    }
}
