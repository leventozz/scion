using System.Runtime.Versioning;
using System.Text;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Editing in place: SKILL.md, other skill files and CLAUDE.md, with the real file system. The files are checked on
/// disk byte for byte, and every screen of the Slice 9 designs is screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class EditingSmokeTests
{
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    private const string OriginalSkill = "---\r\nname: docs\r\ndescription: Writes API docs.\r\n---\r\n# Writing docs\r\n\r\nIntro text.\r\n";

    [Fact]
    public async Task Saving_SKILL_md_keeps_the_BOM_and_CRLF_backs_up_the_old_file_and_leaves_no_scratch_directory()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await app.ScreenshotAsync("edit-skill-view-edit");

        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea");
        await app.ScreenshotAsync("edit-skill-edit");
        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Writes API docs.\n---\n# Edited title\n\nNew intro.\n");
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        var expected = ByteOrderMark.Concat(Encoding.UTF8.GetBytes(
            "---\r\nname: docs\r\ndescription: Writes API docs.\r\n---\r\n# Edited title\r\n\r\nNew intro.\r\n")).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(SkillFile(project)));
        Assert.Equal("Edited title", (await page.TextContentAsync(".file-view .markdown h1"))!.Trim());

        var backupFolder = Assert.Single(Directory.GetDirectories(Path.Combine(app.ScionHome.Path, ".scion", "backups")));
        Assert.StartsWith("SKILL.md-", Path.GetFileName(backupFolder));
        Assert.Equal(SkillFile(project), await File.ReadAllTextAsync(Path.Combine(backupFolder, "source.txt")));
        var backup = Assert.Single(Directory.GetFiles(backupFolder), file => Path.GetFileName(file) != "source.txt");
        Assert.EndsWith("-SKILL.md", backup);
        Assert.Equal(OriginalBytes(), await File.ReadAllBytesAsync(backup));

        Assert.False(Directory.Exists(Path.Combine(project, ".claude", ".scion-tmp")));
        Assert.Equal(
            ["SKILL.md", "scripts"],
            Directory.GetFileSystemEntries(Path.GetDirectoryName(SkillFile(project))!).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Keys_typed_with_no_pause_are_all_kept_in_the_editor()
    {
        const string Typed = "Line one has some words.\nLine two has some more words.\nLine three: the quick brown fox jumps over the lazy dog.\n";
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea");

        var editor = page.Locator("textarea.text-editor-input");
        await editor.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await editor.PressSequentiallyAsync(Typed);

        Assert.Equal(Typed, await editor.InputValueAsync());
        Assert.Equal("1\n2\n3\n4", (await page.TextContentAsync(".text-editor-gutter"))!.Trim());
    }

    [Fact]
    public async Task Ctrl_S_saves_and_stays_in_edit_mode()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);

        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Writes API docs.\n---\n# Saved by shortcut\n");
        Assert.Equal("Unsaved changes", (await page.TextContentAsync(".unsaved-changes"))!.Trim());
        await page.Locator("textarea").PressAsync("Control+s");
        await page.WaitForSelectorAsync(".unsaved-changes", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Contains("# Saved by shortcut", await File.ReadAllTextAsync(SkillFile(project)));
        Assert.True(await page.Locator("textarea").IsVisibleAsync());
        Assert.Equal(["Cancel", "Save"], (await page.Locator(".skill-page-title .button").AllTextContentsAsync()).Select(text => text.Trim()));
    }

    [Fact]
    public async Task An_invalid_name_gets_a_live_amber_warning_before_anything_is_saved()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);

        await page.Locator("textarea").FillAsync("---\nname: Docs\ndescription: Writes API docs.\n---\n# Body\n");

        await page.WaitForSelectorAsync(".skill-page-header .skill-warning");
        Assert.Equal(
            "Name must use lowercase letters, numbers and hyphens.",
            (await page.Locator(".skill-page-header .skill-warning").TextContentAsync())!.Trim());
        Assert.Equal(OriginalBytes(), await File.ReadAllBytesAsync(SkillFile(project)));
    }

    [Fact]
    public async Task Preview_renders_the_draft_like_the_view_does()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);

        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: x\n---\n# Draft heading\n\n```bash\ndotnet test\n```\n");
        await page.Locator(".edit-switch button", new PageLocatorOptions { HasText = "Preview" }).ClickAsync();

        await page.WaitForSelectorAsync(".file-view .markdown h1");
        Assert.Equal("Draft heading", (await page.TextContentAsync(".file-view .markdown h1"))!.Trim());
        Assert.DoesNotContain("Newsreader", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.markdown h1')).fontFamily"));
        Assert.Contains("Geist Mono", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.markdown pre code')).fontFamily"));
        await app.ScreenshotAsync("edit-skill-preview");
    }

    [Fact]
    public async Task Back_with_a_dirty_draft_asks_first_and_Keep_editing_stays_while_Discard_goes_back()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("changed");

        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("Discard your changes?", (await page.TextContentAsync(".dialog h2"))!.Trim());
        Assert.Equal("Your changes to SKILL.md haven't been saved.", (await page.TextContentAsync(".dialog-message"))!.Trim());
        await app.ScreenshotAsync("edit-unsaved-changes");

        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Keep editing" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("changed", await page.Locator("textarea").InputValueAsync());

        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".dialog");
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Discard changes" }).ClickAsync();

        await page.WaitForSelectorAsync(".skill-list");
        Assert.Equal(OriginalBytes(), await File.ReadAllBytesAsync(SkillFile(project)));
    }

    [Fact]
    public async Task Escape_in_the_discard_dialog_keeps_the_draft()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("changed");
        await page.ClickAsync("button[aria-label=Back]");
        await page.WaitForSelectorAsync(".dialog");

        await page.Keyboard.PressAsync("Escape");

        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("changed", await page.Locator("textarea").InputValueAsync());
    }

    [Fact]
    public async Task A_change_on_disk_asks_and_Reload_takes_it_while_Overwrite_replaces_it()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Mine.\n---\n# Mine\n");
        await File.WriteAllTextAsync(SkillFile(project), "---\nname: docs\ndescription: Theirs.\n---\n# Theirs\n");

        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("This file changed on disk", (await page.TextContentAsync(".dialog h2"))!.Trim());
        Assert.Equal("SKILL.md was changed outside Scion after you started editing.", (await page.TextContentAsync(".dialog-message"))!.Trim());
        Assert.Equal(["Cancel", "Reload", "Overwrite"], (await page.Locator(".dialog-footer .button").AllTextContentsAsync()).Select(text => text.Trim()));
        await app.ScreenshotAsync("edit-changed-on-disk");

        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Reload" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Contains("# Theirs", await page.Locator("textarea").InputValueAsync());

        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Merged.\n---\n# Merged\n");
        await File.WriteAllTextAsync(SkillFile(project), "---\nname: docs\ndescription: Again theirs.\n---\n# Again\n");
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog");
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Overwrite" }).ClickAsync();
        await page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Contains("# Merged", await File.ReadAllTextAsync(SkillFile(project)));
        Assert.Equal("Merged", (await page.TextContentAsync(".file-view .markdown h1"))!.Trim());
    }

    [Fact]
    public async Task A_read_only_file_shows_an_amber_error_next_to_the_buttons_and_keeps_the_draft()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: x\n---\n# Draft kept\n");
        File.SetAttributes(SkillFile(project), FileAttributes.ReadOnly);

        try
        {
            await page.Locator(".skill-page-title .button-primary").ClickAsync();

            await page.WaitForSelectorAsync(".skill-page-title .edit-message[role=alert]");
            Assert.Equal(
                "Couldn't save: access to the file was denied.",
                (await page.Locator(".skill-page-title .edit-message").TextContentAsync())!.Trim());
            Assert.Contains("# Draft kept", await page.Locator("textarea").InputValueAsync());
            Assert.Equal(OriginalBytes(), await File.ReadAllBytesAsync(SkillFile(project)));
            Assert.False(Directory.Exists(Path.Combine(project, ".claude", ".scion-tmp")));
            await app.ScreenshotAsync("edit-save-error");
        }
        finally
        {
            File.SetAttributes(SkillFile(project), FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task A_script_is_edited_as_plain_text_and_selecting_another_file_with_changes_asks_first()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "run.sh" }).ClickAsync();
        await page.WaitForSelectorAsync(".text-file");
        await StartEditingAsync(page);
        Assert.Equal(0, await page.Locator(".edit-switch").CountAsync());

        await page.Locator("textarea").FillAsync("echo changed\n");
        await page.Locator(".file-tree-file", new PageLocatorOptions { HasText = "SKILL.md" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog");
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Keep editing" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal("echo changed\n", await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(SkillFile(project))!, "scripts", "run.sh")));
        Assert.Equal(["echo changed"], (await page.Locator(".line-text").AllTextContentsAsync()).Select(text => text.Trim()));
    }

    [Fact]
    public async Task Editing_CLAUDE_md_in_the_tab_saves_it_and_leaves_no_temp_file()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        File.WriteAllText(Path.Combine(project, "CLAUDE.md"), "# Project rules\n\nBe kind.\n");
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator("[role=tab]").Nth(1).ClickAsync();
        await page.WaitForSelectorAsync(".instruction-file");

        await page.Locator(".instruction-file-bar .text-action", new PageLocatorOptions { HasText = "Edit" }).ClickAsync();
        await page.WaitForSelectorAsync("textarea");
        await page.Locator("textarea").FillAsync("# Project rules\n\nBe very kind.\n");
        await app.ScreenshotAsync("edit-instruction-file-edit");
        await page.Locator(".instruction-file-bar .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal("# Project rules\n\nBe very kind.\n", await File.ReadAllTextAsync(Path.Combine(project, "CLAUDE.md")));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(project), entry => Path.GetFileName(entry).Contains(".scion-"));
        Assert.Contains("Be very kind.", await page.TextContentAsync(".instruction-file .markdown"));
        Assert.Single(Directory.GetDirectories(Path.Combine(app.ScionHome.Path, ".scion", "backups")));
    }

    [Fact]
    public async Task Switching_agent_in_the_instruction_tab_with_a_dirty_draft_asks_first()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        File.WriteAllText(Path.Combine(project, "CLAUDE.md"), "# Project rules\n");
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.Locator("[role=tab]").Nth(1).ClickAsync();
        await page.WaitForSelectorAsync(".instruction-file");
        await page.Locator(".instruction-file-bar .text-action", new PageLocatorOptions { HasText = "Edit" }).ClickAsync();
        await page.Locator("textarea").FillAsync("# Draft\n");

        await page.Locator(".agent-switch button", new PageLocatorOptions { HasText = "Codex" }).ClickAsync();

        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("Discard your changes?", (await page.TextContentAsync(".dialog h2"))!.Trim());
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Keep editing" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("Claude", (await page.Locator(".agent-switch button[aria-pressed=true]").TextContentAsync())!.Trim());
        Assert.Equal("# Draft\n", await page.Locator("textarea").InputValueAsync());
    }

    [Fact]
    public async Task Edit_in_the_row_menu_opens_the_skill_already_in_edit_mode()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");

        await page.ClickAsync(".skill-row .options-button");
        await page.Locator(".skill-row [role=menuitem]", new PageLocatorOptions { HasText = "Edit" }).ClickAsync();

        await page.WaitForSelectorAsync("textarea");
        Assert.Contains("# Writing docs", await page.Locator("textarea").InputValueAsync());
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Closing_the_window_with_unsaved_changes_asks_first_and_only_Discard_closes_it()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Writes API docs.\n---\n# Not saved\n");

        app.RequestWindowClose();

        await page.WaitForSelectorAsync(".dialog");
        Assert.Equal("Discard your changes?", (await page.TextContentAsync(".dialog h2"))!.Trim());
        await app.ScreenshotAsync("edit-close-window-discard");
        Assert.False(await app.WaitForExitAsync(TimeSpan.FromSeconds(2)));

        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Keep editing" }).ClickAsync();
        await page.WaitForSelectorAsync(".dialog", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Contains("# Not saved", await page.Locator("textarea").InputValueAsync());
        Assert.False(app.HasExited);

        app.RequestWindowClose();
        await page.WaitForSelectorAsync(".dialog");
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Discard changes" }).ClickAsync();

        Assert.True(await app.WaitForExitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(OriginalBytes(), await File.ReadAllBytesAsync(SkillFile(project)));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Closing_the_window_without_unsaved_changes_just_closes_it()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);

        app.RequestWindowClose();

        Assert.True(await app.WaitForExitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_file_deleted_on_disk_asks_and_Overwrite_writes_it_again()
    {
        using var root = new TempDirectory("scion-edit");
        var project = CreateProject(root);
        await using var app = await StartAsync(project);
        var page = app.Page;
        await OpenSkillAsync(page);
        await StartEditingAsync(page);
        await page.Locator("textarea").FillAsync("---\nname: docs\ndescription: Writes API docs.\n---\n# Back again\n");
        File.Delete(SkillFile(project));

        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog");

        Assert.Equal("This file changed on disk", (await page.TextContentAsync(".dialog h2"))!.Trim());
        Assert.Equal("SKILL.md was deleted outside Scion after you started editing.", (await page.TextContentAsync(".dialog-message"))!.Trim());
        Assert.Equal(["Cancel", "Overwrite"], (await page.Locator(".dialog-footer .button").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.False(File.Exists(SkillFile(project)));
        await app.ScreenshotAsync("edit-deleted-on-disk");

        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Overwrite" }).ClickAsync();
        await page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Contains("# Back again", await File.ReadAllTextAsync(SkillFile(project)));
        Assert.Equal("Back again", (await page.TextContentAsync(".file-view .markdown h1"))!.Trim());
    }

    private static string CreateProject(TempDirectory root)
    {
        var project = Path.Combine(root.Path, "docs-api");
        var skill = Path.Combine(project, ".claude", "skills", "docs");
        Directory.CreateDirectory(Path.Combine(skill, "scripts"));
        File.WriteAllBytes(Path.Combine(skill, "SKILL.md"), OriginalBytes());
        File.WriteAllText(Path.Combine(skill, "scripts", "run.sh"), "echo one\necho two\n");

        return project;
    }

    private static byte[] OriginalBytes() => ByteOrderMark.Concat(Encoding.UTF8.GetBytes(OriginalSkill)).ToArray();

    private static string SkillFile(string project) => Path.Combine(project, ".claude", "skills", "docs", "SKILL.md");

    private static Task<DesktopApp> StartAsync(string project)
    {
        var scionHome = new TempDirectory("scion-home");
        Fixtures.WriteRegistry(scionHome, (project, "docs-api"));

        return DesktopApp.StartAsync(scionHome);
    }

    private static async Task OpenProjectAsync(IPage page)
    {
        await page.WaitForSelectorAsync(".project-card");
        await page.ClickAsync(".project-open");
        await page.WaitForSelectorAsync(".screen-title h1");
    }

    private static async Task OpenSkillAsync(IPage page)
    {
        await OpenProjectAsync(page);
        await page.WaitForSelectorAsync(".skill-row");
        await page.Locator(".skill-row .skill-open").ClickAsync();
        await page.WaitForSelectorAsync(".file-view .markdown");
    }

    private static async Task StartEditingAsync(IPage page)
    {
        await page.Locator(".skill-page-title .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("textarea");
    }
}
