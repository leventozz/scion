using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Scion.Application.Skills;
using Scion.Application.Skills.Copying;
using Scion.Domain;

namespace Scion.UI.Tests;

public class CopySkillsDialogTests : BunitContext
{
    private static readonly DateTimeOffset Noon = UiFakes.Noon;
    private static readonly CopyPlace Api = new("api", new Location.Project("/work/api"));
    private static readonly CopyPlace Web = new("web", new Location.Project("/work/web"));

    private readonly UiFakes _fakes = new();
    private readonly List<CopyReport> _copiedReports = [];
    private int _closedCount;

    public CopySkillsDialogTests()
    {
        _fakes.AddTo(Services);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakes.Registry.Add(new RegisteredProject("/work/api", "api", Noon));
        _fakes.Registry.Add(new RegisteredProject("/work/web", "web", Noon.AddDays(-1)));
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, Skill("docker", "Runs compose files."), Skill("testing", "Runs tests."));
        _fakes.Reader.AddProject("/work/api", Agent.Codex, Skill("for-codex", "Codex only."));
    }

    [Fact]
    public void Is_a_modal_dialog_with_a_title_and_a_close_button()
    {
        var dialog = RenderPull();

        Assert.Equal("true", dialog.Find("[role=dialog]").GetAttribute("aria-modal"));
        Assert.Equal("Copy skills", dialog.Find("h2").TextContent);
        Assert.NotNull(dialog.Find("button[aria-label=Close]"));
    }

    [Fact]
    public void A_pull_starts_with_a_fixed_target_an_empty_source_and_a_disabled_copy_button()
    {
        var dialog = RenderPull();

        Assert.Equal("web · Claude", Row(dialog, "To").QuerySelector(".copy-fixed")!.TextContent);
        Assert.Empty(Row(dialog, "To").QuerySelectorAll(".place-picker, .agent-switch"));
        Assert.Equal("Choose a project", Row(dialog, "From").QuerySelector(".place-picker-button .place-name")!.TextContent);
        Assert.Contains("Choose a project to copy from.", dialog.Find(".copy-empty").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
        Assert.Equal("Copy", CopyButton(dialog).TextContent.Trim());
        Assert.Equal("", dialog.Find(".copy-summary").TextContent);
    }

    [Fact]
    public void The_agent_switch_sits_right_next_to_the_picker_of_the_side_that_can_change()
    {
        var dialog = RenderPull();

        var from = Row(dialog, "From");
        Assert.NotNull(from.QuerySelector(".place-picker"));
        Assert.NotNull(from.QuerySelector(".agent-switch"));
        Assert.Equal("Claude", from.QuerySelector(".agent-switch [aria-pressed=true]")!.TextContent);
    }

    [Fact]
    public void Lists_the_skills_of_the_chosen_source_with_nothing_checked()
    {
        var dialog = RenderPull();

        ChooseSource(dialog, "api");

        var rows = dialog.FindAll(".copy-row");
        Assert.Equal(["docker", "testing"], rows.Select(row => row.QuerySelector(".skill-name")!.TextContent));
        Assert.Equal("Runs compose files.", rows[0].QuerySelector(".skill-description")!.TextContent);
        Assert.All(rows, row => Assert.False(row.QuerySelector("input[type=checkbox]")!.HasAttribute("checked")));
        Assert.Equal("From", dialog.FindAll(".copy-place-label")[0].TextContent);
    }

    [Fact]
    public void Shows_a_warning_icon_on_a_skill_with_warnings()
    {
        _fakes.Reader.AddProject("/work/api", Agent.ClaudeCode, new SkillFolder("Bad_Name", "Bad_Name", "Fine."));
        var dialog = RenderPull();

        ChooseSource(dialog, "api");

        Assert.NotNull(dialog.Find(".copy-row .warning-icon"));
    }

    [Fact]
    public void Says_so_when_the_source_has_no_skills_for_the_agent()
    {
        var dialog = RenderPull();

        ChooseSource(dialog, "web");

        Assert.Equal("No Claude skills in web.", dialog.Find(".copy-empty").TextContent);
        Assert.Empty(dialog.FindAll(".copy-row"));
    }

    [Fact]
    public void Switching_the_source_agent_lists_the_skills_of_that_agent_and_forgets_the_checks()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        Check(dialog, "docker");

        Row(dialog, "From").QuerySelectorAll(".agent-switch button")[1].Click();

        Assert.Equal(["for-codex"], dialog.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
        Assert.Equal("0 skills will be copied to web.", dialog.Find(".copy-summary").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Checking_a_skill_updates_the_summary_and_enables_the_copy_button()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");

        Check(dialog, "docker");

        Assert.Equal("1 skill will be copied to web.", dialog.Find(".copy-summary").TextContent);
        Assert.Equal("Copy 1 skill", CopyButton(dialog).TextContent.Trim());
        Assert.False(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Select_all_checks_every_listed_skill()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");

        TextAction(dialog, "Select all").Click();

        Assert.Equal("Copy 2 skills", CopyButton(dialog).TextContent.Trim());
    }

    [Fact]
    public void The_filter_narrows_the_list_and_select_all_checks_only_what_is_shown()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");

        dialog.Find(".copy-filter").Input("TEST");
        TextAction(dialog, "Select all").Click();

        Assert.Equal(["testing"], dialog.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
        Assert.Equal("Copy 1 skill", CopyButton(dialog).TextContent.Trim());
    }

    [Fact]
    public void Marks_a_checked_skill_that_the_target_already_has_with_a_skip_default_choice()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old docker."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");

        Check(dialog, "docker");
        Check(dialog, "testing");

        var conflictRow = RowOf(dialog, "docker");
        Assert.Equal("Already in web, different content", conflictRow.QuerySelector(".skill-warning")!.TextContent);
        var choice = conflictRow.QuerySelectorAll(".conflict-choice button");
        Assert.Equal(["Skip", "Overwrite"], choice.Select(button => button.TextContent));
        Assert.Equal("true", choice[0].GetAttribute("aria-pressed"));
        Assert.Empty(RowOf(dialog, "testing").QuerySelectorAll(".conflict-choice, .skill-warning"));
        Assert.Equal("1 skill will be copied to web, 1 skipped.", dialog.Find(".copy-summary").TextContent);
    }

    [Fact]
    public void Shows_a_checked_skill_with_identical_content_as_identical_without_a_choice()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Runs compose files."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");

        Check(dialog, "docker");

        var row = RowOf(dialog, "docker");
        var note = row.QuerySelector(".skill-note")!;
        Assert.Equal("Identical in web", note.TextContent);
        Assert.Empty(row.QuerySelectorAll(".conflict-choice, .skill-warning"));
        Assert.DoesNotContain(
            dialog.FindAll(".text-action"), action => action.TextContent.Trim() is "Skip all" or "Overwrite all");
        Assert.Equal("0 skills will be copied to web, 1 already identical.", dialog.Find(".copy-summary").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Does_not_write_an_identical_skill_but_copies_the_rest_and_closes()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Runs compose files."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(["testing"], _fakes.SkillFolders.Copied);
        Assert.Empty(_fakes.SkillFolders.Replaced);
        Assert.Equal(1, _closedCount);
    }

    [Fact]
    public void Shows_no_conflict_for_a_skill_that_is_not_checked()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old docker."));
        var dialog = RenderPull();

        ChooseSource(dialog, "api");

        Assert.Empty(dialog.FindAll(".conflict-choice"));
        Assert.DoesNotContain(
            dialog.FindAll(".text-action"), action => action.TextContent.Trim() is "Skip all" or "Overwrite all");
    }

    [Fact]
    public void Choosing_overwrite_on_a_conflict_counts_it_as_copied_and_overwritten()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old docker."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        Check(dialog, "docker");

        RowOf(dialog, "docker").QuerySelectorAll(".conflict-choice button")[1].Click();

        Assert.Equal("1 skill will be copied to web, 1 overwritten.", dialog.Find(".copy-summary").TextContent);
        Assert.Equal("true", RowOf(dialog, "docker").QuerySelectorAll(".conflict-choice button")[1].GetAttribute("aria-pressed"));
        Assert.False(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Overwrite_all_and_skip_all_apply_to_every_checked_conflict()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old."), Skill("testing", "Old."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        TextAction(dialog, "Overwrite all").Click();
        Assert.Equal("2 skills will be copied to web, 2 overwritten.", dialog.Find(".copy-summary").TextContent);

        TextAction(dialog, "Skip all").Click();
        Assert.Equal("0 skills will be copied to web, 2 skipped.", dialog.Find(".copy-summary").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void Copying_writes_the_skills_reports_them_and_closes_the_dialog()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(["docker", "testing"], _fakes.SkillFolders.Copied);
        var report = Assert.Single(_copiedReports);
        Assert.Equal(
            [new CopiedSkill("docker", SkillCopyOutcome.Copied), new CopiedSkill("testing", SkillCopyOutcome.Copied)],
            report.Skills);
        Assert.Equal(1, _closedCount);
    }

    [Fact]
    public void A_skipped_conflict_is_left_alone()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(["testing"], _fakes.SkillFolders.Copied);
        Assert.Empty(_fakes.SkillFolders.Replaced);
    }

    [Fact]
    public void An_overwritten_conflict_is_replaced()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old."));
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        Check(dialog, "docker");
        TextAction(dialog, "Overwrite all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(["docker"], _fakes.SkillFolders.Replaced);
        Assert.Equal(SkillCopyOutcome.Overwritten, Assert.Single(_copiedReports).Skills.Single().Outcome);
    }

    [Fact]
    public void Stays_open_when_a_skill_fails_drops_the_copied_rows_and_marks_the_failed_one()
    {
        _fakes.SkillFolders.FailOn("testing");
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(0, _closedCount);
        Assert.Equal(["testing"], dialog.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
        Assert.Equal("Couldn't copy", RowOf(dialog, "testing").QuerySelector(".skill-warning")!.TextContent);
        Assert.Equal("Copy 1 skill", CopyButton(dialog).TextContent.Trim());
    }

    [Fact]
    public void Reports_what_was_written_even_when_another_skill_failed_so_the_screen_behind_can_refresh()
    {
        _fakes.SkillFolders.FailOn("testing");
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();

        CopyButton(dialog).Click();

        Assert.Equal(
            [SkillCopyOutcome.Copied, SkillCopyOutcome.Failed],
            Assert.Single(_copiedReports).Skills.Select(skill => skill.Outcome));
    }

    [Fact]
    public void Copying_again_after_a_failure_retries_only_the_failed_skill_and_then_closes()
    {
        _fakes.SkillFolders.FailOn("testing");
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        TextAction(dialog, "Select all").Click();
        CopyButton(dialog).Click();
        _fakes.SkillFolders.StopFailing();

        CopyButton(dialog).Click();

        Assert.Equal(["docker", "testing"], _fakes.SkillFolders.Copied);
        Assert.Equal(1, _closedCount);
    }

    [Fact]
    public void Shows_an_amber_line_when_the_target_folder_has_disappeared()
    {
        var dialog = RenderPull();
        ChooseSource(dialog, "api");
        Check(dialog, "docker");
        _fakes.Folders.MarkMissing("/work/web");

        CopyButton(dialog).Click();

        Assert.Equal("The target folder no longer exists.", dialog.Find(".copy-error").TextContent);
        Assert.Equal(0, _closedCount);
    }

    [Fact]
    public void Refuses_to_copy_a_place_onto_itself()
    {
        var dialog = RenderPull();

        ChooseSource(dialog, "web");

        Assert.Equal("Choose a different project or agent.", dialog.Find(".copy-summary").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_push_starts_with_a_fixed_source_the_skill_checked_and_no_target()
    {
        var dialog = RenderPush("docker");

        Assert.Equal("api · Claude", Row(dialog, "From").QuerySelector(".copy-fixed")!.TextContent);
        Assert.Empty(Row(dialog, "From").QuerySelectorAll(".place-picker, .agent-switch"));
        Assert.Equal("Choose a project", Row(dialog, "To").QuerySelector(".place-picker-button .place-name")!.TextContent);
        Assert.Equal(["docker", "testing"], dialog.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
        Assert.True(RowOf(dialog, "docker").QuerySelector("input[type=checkbox]")!.HasAttribute("checked"));
        Assert.Equal("Choose a project to copy to.", dialog.Find(".copy-summary").TextContent);
        Assert.True(CopyButton(dialog).HasAttribute("disabled"));
    }

    [Fact]
    public void A_push_copies_to_the_chosen_target_and_marks_conflicts_there()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old."));
        var dialog = RenderPush("docker");

        ChooseTarget(dialog, "web");

        Assert.Equal("Already in web, different content", RowOf(dialog, "docker").QuerySelector(".skill-warning")!.TextContent);
        Assert.Equal("0 skills will be copied to web, 1 skipped.", dialog.Find(".copy-summary").TextContent);
    }

    [Fact]
    public void A_push_can_target_the_other_agent_of_the_same_project()
    {
        var dialog = RenderPush("docker");

        ChooseTarget(dialog, "api");
        Row(dialog, "To").QuerySelectorAll(".agent-switch button")[1].Click();
        CopyButton(dialog).Click();

        Assert.Equal(["docker"], _fakes.SkillFolders.Copied);
        Assert.Single(_copiedReports);
    }

    [Fact]
    public void Changing_the_target_forgets_an_overwrite_decision_made_for_the_old_one()
    {
        _fakes.Reader.AddProject("/work/web", Agent.ClaudeCode, Skill("docker", "Old."));
        _fakes.Reader.AddPersonal(Agent.ClaudeCode, Skill("docker", "Old personal."));
        var dialog = RenderPush("docker");
        ChooseTarget(dialog, "web");
        RowOf(dialog, "docker").QuerySelectorAll(".conflict-choice button")[1].Click();

        ChooseTarget(dialog, "Personal");

        Assert.Equal("true", RowOf(dialog, "docker").QuerySelectorAll(".conflict-choice button")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_pull_from_the_other_agent_of_the_same_place_lists_that_agents_skills_at_once()
    {
        var dialog = Render<CopySkillsDialog>(parameters => parameters
            .Add(p => p.Start, CopyDialogStart.CopyFromAgent(Api, Agent.ClaudeCode, Agent.Codex))
            .Add(p => p.OnClose, () => _closedCount++)
            .Add(p => p.OnCopied, report => _copiedReports.Add(report)));

        Assert.Equal(["for-codex"], dialog.FindAll(".copy-row .skill-name").Select(name => name.TextContent));
        Assert.Equal("Codex", Row(dialog, "From").QuerySelector(".agent-switch [aria-pressed=true]")!.TextContent);
        Assert.Equal("api · Claude", Row(dialog, "To").QuerySelector(".copy-fixed")!.TextContent);
    }

    [Fact]
    public void Escape_closes_the_dialog()
    {
        var dialog = RenderPull();

        dialog.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(1, _closedCount);
    }

    [Fact]
    public void The_close_button_and_cancel_close_the_dialog()
    {
        var dialog = RenderPull();

        dialog.Find("button[aria-label=Close]").Click();
        dialog.FindAll(".dialog-footer button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        Assert.Equal(2, _closedCount);
    }

    [Fact]
    public void A_click_on_the_backdrop_does_not_close_the_dialog()
    {
        var dialog = RenderPull();

        // The backdrop listens to nothing, so a click on it cannot close the dialog.
        Assert.Throws<MissingEventHandlerException>(() => dialog.Find(".dialog-backdrop").Click());

        Assert.Equal(0, _closedCount);
        Assert.NotNull(dialog.Find("[role=dialog]"));
    }

    [Fact]
    public void Escape_inside_the_open_project_picker_closes_the_picker_and_not_the_dialog()
    {
        var dialog = RenderPull();
        dialog.Find(".place-picker-button").Click();

        dialog.Find(".place-picker-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(dialog.FindAll(".place-picker-panel"));
        Assert.Equal(0, _closedCount);
    }

    private IRenderedComponent<CopySkillsDialog> RenderPull() => RenderDialog(CopyDialogStart.CopyFrom(Web, Agent.ClaudeCode));

    private IRenderedComponent<CopySkillsDialog> RenderPush(string checkedFolder) =>
        RenderDialog(CopyDialogStart.CopyTo(Api, Agent.ClaudeCode, checkedFolder));

    private IRenderedComponent<CopySkillsDialog> RenderDialog(CopyDialogStart start) =>
        Render<CopySkillsDialog>(parameters => parameters
            .Add(p => p.Start, start)
            .Add(p => p.OnClose, () => _closedCount++)
            .Add(p => p.OnCopied, report => _copiedReports.Add(report)));

    private static IElement Row(IRenderedComponent<CopySkillsDialog> dialog, string label) =>
        dialog.FindAll(".copy-place-row").Single(row => row.QuerySelector(".copy-place-label")!.TextContent == label);

    private static void ChooseSource(IRenderedComponent<CopySkillsDialog> dialog, string name) =>
        Choose(dialog, "From", name);

    private static void ChooseTarget(IRenderedComponent<CopySkillsDialog> dialog, string name) =>
        Choose(dialog, "To", name);

    private static void Choose(IRenderedComponent<CopySkillsDialog> dialog, string label, string name)
    {
        Row(dialog, label).QuerySelector(".place-picker-button")!.Click();
        dialog.FindAll(".place-option")
            .Single(option => option.QuerySelector(".place-option-name")!.TextContent == name)
            .Click();
    }

    private static IElement RowOf(IRenderedComponent<CopySkillsDialog> dialog, string skillName) =>
        dialog.FindAll(".copy-row").Single(row => row.QuerySelector(".skill-name")!.TextContent == skillName);

    private static void Check(IRenderedComponent<CopySkillsDialog> dialog, string skillName) =>
        RowOf(dialog, skillName).QuerySelector("input[type=checkbox]")!.Change(true);

    private static IElement CopyButton(IRenderedComponent<CopySkillsDialog> dialog) => dialog.Find(".button-primary");

    private static IElement TextAction(IRenderedComponent<CopySkillsDialog> dialog, string text) =>
        dialog.FindAll(".text-action").Single(action => action.TextContent.Trim() == text);

    private static SkillFolder Skill(string name, string description) => new(name, name, description);
}
