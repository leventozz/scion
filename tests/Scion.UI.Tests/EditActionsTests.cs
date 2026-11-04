using Bunit;

namespace Scion.UI.Tests;

public class EditActionsTests : BunitContext
{
    private int _cancelled;
    private int _saved;

    [Fact]
    public void Has_a_secondary_Cancel_and_the_one_primary_Save()
    {
        var actions = RenderActions();

        Assert.Equal("Cancel", actions.Find(".button-secondary").TextContent);
        Assert.Equal("Save", actions.Find(".button-primary").TextContent);
        Assert.Single(actions.FindAll(".button-primary"));
    }

    [Fact]
    public void Says_Unsaved_changes_in_normal_case_only_when_the_draft_is_dirty()
    {
        Assert.Empty(RenderActions(isDirty: false).FindAll(".unsaved-changes"));
        Assert.Equal("Unsaved changes", RenderActions(isDirty: true).Find(".unsaved-changes").TextContent);
    }

    [Fact]
    public void Shows_an_error_as_an_amber_alert_with_a_warning_icon()
    {
        var actions = RenderActions(error: "Couldn't save the file.");

        var message = actions.Find(".edit-message");
        Assert.Equal("alert", message.GetAttribute("role"));
        Assert.Equal("Couldn't save the file.", message.TextContent.Trim());
        Assert.NotNull(message.QuerySelector(".warning-icon"));
    }

    [Fact]
    public void Shows_a_note_next_to_the_buttons()
    {
        var actions = RenderActions(note: "Saved, but the previous version couldn't be backed up.");

        Assert.Equal("Saved, but the previous version couldn't be backed up.", actions.Find(".edit-message").TextContent.Trim());
    }

    [Fact]
    public void Shows_no_message_when_there_is_none()
    {
        Assert.Empty(RenderActions().FindAll(".edit-message"));
    }

    [Fact]
    public void An_error_wins_over_a_note()
    {
        var actions = RenderActions(error: "Couldn't save the file.", note: "A note.");

        Assert.Equal("Couldn't save the file.", Assert.Single(actions.FindAll(".edit-message")).TextContent.Trim());
    }

    [Fact]
    public void Calls_back_on_Cancel_and_on_Save()
    {
        var actions = RenderActions();

        actions.Find(".button-secondary").Click();
        actions.Find(".button-primary").Click();

        Assert.Equal(1, _cancelled);
        Assert.Equal(1, _saved);
    }

    [Fact]
    public void Save_is_disabled_while_saving()
    {
        var actions = RenderActions(isSaving: true);

        Assert.True(actions.Find(".button-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void Save_is_enabled_otherwise()
    {
        Assert.False(RenderActions().Find(".button-primary").HasAttribute("disabled"));
    }

    private IRenderedComponent<EditActions> RenderActions(
        bool isDirty = false, bool isSaving = false, string? error = null, string? note = null) =>
        Render<EditActions>(parameters => parameters
            .Add(actions => actions.IsDirty, isDirty)
            .Add(actions => actions.IsSaving, isSaving)
            .Add(actions => actions.Error, error)
            .Add(actions => actions.Note, note)
            .Add(actions => actions.OnCancel, () => _cancelled++)
            .Add(actions => actions.OnSave, () => _saved++));
}
