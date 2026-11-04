using Bunit;
using Microsoft.AspNetCore.Components.Web;

namespace Scion.UI.Tests;

public class EditDialogsTests : BunitContext
{
    private int _keepEditing;
    private int _discard;
    private int _cancel;
    private int _reload;
    private int _overwrite;

    public EditDialogsTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void The_discard_dialog_names_the_file_and_offers_the_two_choices()
    {
        var dialog = RenderDiscard("SKILL.md");

        Assert.Equal("Discard your changes?", dialog.Find("h2").TextContent);
        Assert.Equal("Your changes to SKILL.md haven't been saved.", dialog.Find(".dialog-message").TextContent);
        Assert.Equal("Discard changes", dialog.Find(".dialog-footer .button-secondary").TextContent);
        Assert.Equal("Keep editing", dialog.Find(".dialog-footer .button-primary").TextContent);
    }

    [Fact]
    public void The_discard_dialog_calls_back_for_each_choice()
    {
        var dialog = RenderDiscard("SKILL.md");

        dialog.Find(".dialog-footer .button-secondary").Click();
        dialog.Find(".dialog-footer .button-primary").Click();

        Assert.Equal(1, _discard);
        Assert.Equal(1, _keepEditing);
    }

    [Fact]
    public void The_discard_dialog_treats_the_close_button_and_Escape_as_keeping_the_draft()
    {
        var dialog = RenderDiscard("SKILL.md");

        dialog.Find("button[aria-label=Close]").Click();
        dialog.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(3, _keepEditing);
        Assert.Equal(0, _discard);
    }

    [Fact]
    public void A_click_on_the_backdrop_does_not_close_the_dialog()
    {
        var dialog = RenderDiscard("SKILL.md");

        // The backdrop listens to nothing but keys, so a click on it cannot close the dialog.
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());

        Assert.Equal(0, _keepEditing);
        Assert.Equal(0, _discard);
    }

    [Fact]
    public void Other_keys_do_nothing()
    {
        var dialog = RenderDiscard("SKILL.md");

        dialog.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(0, _keepEditing);
    }

    [Fact]
    public void A_dialog_is_modal_and_named_by_its_title()
    {
        var dialog = RenderDiscard("SKILL.md").Find("[role=dialog]");

        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal(dialog.GetAttribute("aria-labelledby"), dialog.QuerySelector("h2")!.Id);
    }

    [Fact]
    public void Takes_the_focus_when_it_opens()
    {
        var dialog = RenderDiscard("SKILL.md");

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(dialog.Find("[role=dialog]"));
    }

    [Fact]
    public void The_changed_on_disk_dialog_offers_Cancel_Reload_and_Overwrite_all_secondary()
    {
        var dialog = RenderChangedOnDisk("SKILL.md", DiskConflict.Changed);

        Assert.Equal("This file changed on disk", dialog.Find("h2").TextContent);
        Assert.Equal("SKILL.md was changed outside Scion after you started editing.", dialog.Find(".dialog-message").TextContent);
        Assert.Equal(
            ["Cancel", "Reload", "Overwrite"],
            dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent));
        Assert.Empty(dialog.FindAll(".button-primary"));
    }

    [Fact]
    public void The_changed_on_disk_dialog_calls_back_for_each_choice()
    {
        var dialog = RenderChangedOnDisk("SKILL.md", DiskConflict.Changed);

        foreach (var button in dialog.FindAll(".dialog-footer .button"))
        {
            button.Click();
        }

        Assert.Equal((1, 1, 1), (_cancel, _reload, _overwrite));
    }

    [Fact]
    public void A_deleted_file_says_so_and_cannot_be_reloaded()
    {
        var dialog = RenderChangedOnDisk("SKILL.md", DiskConflict.Deleted);

        Assert.Equal("SKILL.md was deleted outside Scion after you started editing.", dialog.Find(".dialog-message").TextContent);
        Assert.Equal(
            ["Cancel", "Overwrite"],
            dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent));
    }

    [Fact]
    public void The_changed_on_disk_dialog_treats_the_close_button_and_Escape_as_cancel()
    {
        var dialog = RenderChangedOnDisk("SKILL.md", DiskConflict.Changed);

        dialog.Find("button[aria-label=Close]").Click();
        dialog.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());

        Assert.Equal(2, _cancel);
        Assert.Equal(0, _overwrite);
    }

    private IRenderedComponent<DiscardChangesDialog> RenderDiscard(string fileName) =>
        Render<DiscardChangesDialog>(parameters => parameters
            .Add(dialog => dialog.FileName, fileName)
            .Add(dialog => dialog.OnKeepEditing, () => _keepEditing++)
            .Add(dialog => dialog.OnDiscard, () => _discard++));

    private IRenderedComponent<ChangedOnDiskDialog> RenderChangedOnDisk(string fileName, DiskConflict conflict) =>
        Render<ChangedOnDiskDialog>(parameters => parameters
            .Add(dialog => dialog.FileName, fileName)
            .Add(dialog => dialog.Conflict, conflict)
            .Add(dialog => dialog.OnCancel, () => _cancel++)
            .Add(dialog => dialog.OnReload, () => _reload++)
            .Add(dialog => dialog.OnOverwrite, () => _overwrite++));
}
