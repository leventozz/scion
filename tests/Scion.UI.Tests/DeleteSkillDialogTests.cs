using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Deleting;
using Scion.Application.Skills;
using Scion.Domain;

namespace Scion.UI.Tests;

public class DeleteSkillDialogTests : BunitContext
{
    private static readonly Location Project = new Location.Project("/work/api");

    private readonly UiFakes _fakes = new();
    private int _closed;
    private int _deleted;

    public DeleteSkillDialogTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("docker", "docker", "Runs compose files."));
    }

    [Fact]
    public void Is_a_modal_dialog_that_asks_about_the_skill_in_italics_and_says_where_it_lives()
    {
        var dialog = Render();

        Assert.Equal("true", dialog.Find("[role=dialog]").GetAttribute("aria-modal"));
        Assert.Equal("Delete docker?", dialog.Find("h2").TextContent);
        Assert.Equal("docker", dialog.Find("h2 em").TextContent);
        Assert.Equal(DeleteMessages.Body, dialog.Find(".dialog-text").TextContent);
        Assert.Equal("api · Claude", dialog.Find(".dialog-context").TextContent);
        Assert.NotNull(dialog.Find("button[aria-label=Close]"));
    }

    [Fact]
    public void Names_personal_and_the_other_agent_in_the_context_line()
    {
        var dialog = Render(new Location.Personal(), "Personal", Agent.Codex);

        Assert.Equal("Personal · Codex", dialog.Find(".dialog-context").TextContent);
    }

    [Fact]
    public void Offers_Delete_first_and_Cancel_as_the_one_primary_button()
    {
        var dialog = Render();

        Assert.Equal(["Delete", "Cancel"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
        Assert.Equal(["Cancel"], dialog.FindAll(".button-primary").Select(button => button.TextContent.Trim()));
        Assert.Equal(["Delete"], dialog.FindAll(".button-secondary").Select(button => button.TextContent.Trim()));
    }

    [Fact]
    public void Puts_the_focus_on_the_dialog_so_Enter_deletes_nothing()
    {
        var dialog = Render();

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(dialog.Find("[role=dialog]"));
    }

    [Fact]
    public void The_close_button_Cancel_and_Esc_close_it_without_deleting_anything()
    {
        var dialog = Render();

        dialog.Find("button[aria-label=Close]").Click();
        Button(dialog, "Cancel").Click();
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(3, _closed);
        Assert.Equal(0, _deleted);
        Assert.Empty(_fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void A_click_on_the_backdrop_closes_nothing()
    {
        var dialog = Render();

        // The backdrop listens to nothing, so a click on it cannot close the dialog.
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());

        Assert.Equal(0, _closed);
    }

    [Fact]
    public void Delete_moves_the_skill_to_the_trash_and_tells_the_screen()
    {
        var dialog = Render();

        Button(dialog, "Delete").Click();

        dialog.WaitForAssertion(() => Assert.Equal(1, _deleted));
        Assert.Equal(["docker"], _fakes.SkillTrash.Trashed);
        Assert.Equal(0, _closed);
    }

    [Fact]
    public void While_it_deletes_both_buttons_are_off_so_a_second_click_deletes_nothing_more()
    {
        var gate = _fakes.SkillTrash.HoldMove();
        var dialog = Render();

        Button(dialog, "Delete").Click();

        Assert.True(Button(dialog, "Delete").HasAttribute("disabled"));
        Assert.True(Button(dialog, "Cancel").HasAttribute("disabled"));
        gate.SetResult();
        dialog.WaitForAssertion(() => Assert.Equal(1, _deleted));
        Assert.Single(_fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void A_skill_that_is_already_gone_counts_as_deleted()
    {
        var dialog = Render();
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode);

        Button(dialog, "Delete").Click();

        dialog.WaitForAssertion(() => Assert.Equal(1, _deleted));
        Assert.Empty(dialog.FindAll(".field-problem"));
    }

    [Theory]
    [InlineData(SkillTrashFailure.Other)]
    [InlineData(SkillTrashFailure.Linked)]
    public void A_failed_delete_says_so_next_to_the_buttons_and_lets_the_user_try_again(SkillTrashFailure failure)
    {
        var dialog = Render();
        _fakes.SkillTrash.FailWith(failure);

        Button(dialog, "Delete").Click();

        dialog.WaitForAssertion(() => Assert.Equal("The skill couldn't be deleted.", dialog.Find(".dialog-footer .field-problem").TextContent));
        Assert.NotNull(dialog.Find(".dialog-footer .field-problem .warning-icon"));
        Assert.Equal(0, _deleted);
        Assert.False(Button(dialog, "Delete").HasAttribute("disabled"));
        Assert.False(Button(dialog, "Cancel").HasAttribute("disabled"));

        _fakes.SkillTrash.FailWith(null);
        Button(dialog, "Delete").Click();
        dialog.WaitForAssertion(() => Assert.Equal(1, _deleted));
        Assert.Equal(["docker"], _fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void A_half_removed_original_says_the_skill_is_in_the_trash_and_offers_only_Close()
    {
        _fakes.SkillTrash.LeaveOriginalBehind();
        var dialog = Render();

        Button(dialog, "Delete").Click();

        dialog.WaitForAssertion(() => Assert.Equal(
            "Moved to Scion's trash, but some files couldn't be removed from the skill folder.",
            dialog.Find(".dialog-footer .field-problem").TextContent));
        Assert.Equal(["Close"], dialog.FindAll(".dialog-footer .button").Select(button => button.TextContent.Trim()));
        Assert.Equal(0, _deleted);
        Assert.Equal(["docker"], _fakes.SkillTrash.Trashed);
    }

    [Fact]
    public void Leaving_the_half_removed_state_tells_the_screen_so_the_list_is_refreshed()
    {
        _fakes.SkillTrash.LeaveOriginalBehind();
        var dialog = Render();
        Button(dialog, "Delete").Click();
        dialog.WaitForAssertion(() => Assert.Single(dialog.FindAll(".field-problem")));

        Button(dialog, "Close").Click();
        dialog.Find("button[aria-label=Close]").Click();
        dialog.Find(".dialog-backdrop").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(3, _deleted);
        Assert.Equal(0, _closed);
    }

    private IRenderedComponent<DeleteSkillDialog> Render(Location? location = null, string placeName = "api", Agent agent = Agent.ClaudeCode) =>
        Render<DeleteSkillDialog>(parameters => parameters
            .Add(dialog => dialog.Location, location ?? Project)
            .Add(dialog => dialog.PlaceName, placeName)
            .Add(dialog => dialog.Agent, agent)
            .Add(dialog => dialog.Skill, new SkillSummary("docker", "docker", "Runs compose files.", [], CanBeDeleted: true))
            .Add(dialog => dialog.OnClose, () => _closed++)
            .Add(dialog => dialog.OnDeleted, () => _deleted++));

    private static IElement Button(IRenderedComponent<DeleteSkillDialog> dialog, string text) =>
        dialog.FindAll(".dialog-footer .button").Single(button => button.TextContent.Trim() == text);
}
