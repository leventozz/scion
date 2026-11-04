using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Adapting a skill with AI against the real desktop app and the real file system, with a stand-in for <c>claude</c>.
/// The screens of the Slice 12 designs are screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class AdaptingSmokeTests
{
    private const string SourceSkill = "---\nname: compose\ndescription: Checks compose files.\n---\n# Compose\nUses MongoDB.\n";
    private const string AdaptedSkill = "---\nname: renamed-by-the-model\ndescription: Checks compose files.\n---\n# Compose\nUses PostgreSQL.\n";
    private static readonly byte[] ScriptBytes = [0x23, 0x21, 0x0D, 0x0A, 0x00, 0xFF, 0x80];

    private static object Success => new { signedIn = true, authMethod = "claude.ai", adapt = new { kind = "success", skillFile = AdaptedSkill } };

    [Fact]
    public async Task The_row_menu_offers_Adapt_with_AI_after_Copy_to_and_before_the_separator()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenSourceAsync(app);

        await world.RowMenuAsync(app);

        await app.ScreenshotAsync("adapt-row-menu");
        var items = (await app.Page.Locator(".skill-row [role=menuitem], .skill-row [role=separator]").EvaluateAllAsync<string[]>(
            "elements => elements.map(element => element.getAttribute('role') == 'separator' ? '-' : element.textContent.trim())"));
        Assert.Equal(["Edit", "Copy to…", "Adapt with AI…", "-", "Delete"], items);
    }

    [Fact]
    public async Task What_the_dialog_lists_is_exactly_what_claude_code_receives()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator("textarea.adapt-instructions").FillAsync("We use PostgreSQL.");

        await app.ScreenshotAsync("adapt-dialog");
        Assert.Equal("· Connected via Claude Code", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.Equal("Counts toward your Claude plan.", (await page.Locator(".adapt-provider-note").TextContentAsync())!.Trim());
        Assert.Equal(4, await page.Locator(".shared-list li").CountAsync());
        await page.Locator(".shared-toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.shared-list li').length === 5");
        var listed =(await page.Locator(".shared-list li").AllTextContentsAsync())
            .Select(text => Regex.Replace(text.Trim(), @" · \d+ KB$", ""))
            .ToArray();
        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");

        var prompt = Assert.Single(world.Fake.SentPrompts());
        var titles = Regex.Matches(prompt, @"^<<< (.+) >>>$", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(listed, titles);
        Assert.Contains("We use PostgreSQL.", prompt);
        Assert.DoesNotContain("Program.cs", prompt);
        Assert.DoesNotContain("SECRET", prompt);
    }

    [Fact]
    public async Task What_is_left_out_is_listed_as_not_shared_in_the_dialog_and_never_reaches_claude_code()
    {
        using var world = new World(Success);
        // 40 KB is over the limit for one file; the other four are within it, but the last of them does not fit in the total.
        File.WriteAllText(Path.Combine(world.TargetProject, "package.json"), new string('P', 40 * 1024));
        foreach (var (name, filler) in new[] { ("Cargo.toml", 'C'), ("build.gradle", 'B'), ("go.mod", 'G'), ("pom.xml", 'M'), ("pyproject.toml", 'Y') })
        {
            File.WriteAllText(Path.Combine(world.TargetProject, name), new string(filler, 30 * 1024));
        }

        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.WaitForSelectorAsync(".shared-omitted li");

        await app.ScreenshotAsync("adapt-not-shared");
        Assert.Equal(
            ["Not shared (over 32 KB): package.json", "Not shared (total limit reached): pyproject.toml"],
            (await page.Locator(".shared-omitted li").AllTextContentsAsync()).Select(text => text.Trim()).ToArray());
        Assert.True(await page.Locator(".shared-omitted").EvaluateAsync<bool>("list => parseFloat(getComputedStyle(list).opacity) < 1"));
        await page.Locator(".shared-toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.shared-list li').length > 3");
        var listed = (await page.Locator(".shared-list li").AllTextContentsAsync()).Select(text => text.Trim()).ToArray();
        Assert.DoesNotContain(listed, line => line.Contains("package.json") || line.Contains("pyproject.toml"));
        Assert.Contains(listed, line => line.StartsWith("Cargo.toml"));

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");

        var prompt = Assert.Single(world.Fake.SentPrompts());
        Assert.DoesNotContain("package.json", prompt);
        Assert.DoesNotContain("pyproject.toml", prompt);
        Assert.DoesNotContain("PPPPPPPP", prompt);
        Assert.DoesNotContain("YYYYYYYY", prompt);
        Assert.Contains("<<< Cargo.toml >>>", prompt);
        Assert.Contains("MMMMMMMM", prompt);
        var titles = Regex.Matches(prompt, @"^<<< (.+) >>>$", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(listed.Select(line => Regex.Replace(line, @" · \d+ KB$", "")).ToArray(), titles);
    }

    [Fact]
    public async Task Saving_writes_the_adapted_skill_with_the_forced_name_and_copies_the_scripts_unchanged()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");
        await app.ScreenshotAsync("adapt-result");
        await page.Locator(".dialog-footer .button-primary", new PageLocatorOptions { HasText = "Save to web-b" }).ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        var target = Path.Combine(world.TargetProject, ".claude", "skills", "compose");
        var skill = await File.ReadAllTextAsync(Path.Combine(target, "SKILL.md"));
        Assert.Equal(AdaptedSkill.Replace("renamed-by-the-model", "compose"), skill);
        Assert.Equal(ScriptBytes, await File.ReadAllBytesAsync(Path.Combine(target, "scripts", "run.bin")));
        Assert.Equal(SourceSkill, await File.ReadAllTextAsync(Path.Combine(world.SourceProject, ".claude", "skills", "compose", "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".claude", ".scion-tmp")));
    }

    [Fact]
    public async Task A_skill_that_the_target_has_is_only_replaced_by_an_explicit_overwrite()
    {
        using var world = new World(Success);
        var existing = Path.Combine(world.TargetProject, ".claude", "skills", "compose");
        Directory.CreateDirectory(existing);
        await File.WriteAllTextAsync(Path.Combine(existing, "SKILL.md"), "---\nname: compose\ndescription: Mine.\n---\nMine.\n");
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".review-problem");
        await app.ScreenshotAsync("adapt-result-exists");

        Assert.Equal("A skill with this name already exists in web-b.", (await page.Locator(".review-problem").TextContentAsync())!.Trim());
        Assert.Equal("Mine.\n", (await File.ReadAllTextAsync(Path.Combine(existing, "SKILL.md"))).Split('\n', 5)[4]);

        await page.Locator(".dialog-footer .button-primary", new PageLocatorOptions { HasText = "Overwrite in web-b" }).ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Contains("Uses PostgreSQL.", await File.ReadAllTextAsync(Path.Combine(existing, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(existing, "scripts", "run.bin")));
    }

    [Theory]
    [InlineData("api_key", "Uses your Anthropic API key.")]
    [InlineData("api_key_helper", "Uses your Anthropic API key.")]
    [InlineData("oauth_token", "Counts toward your Claude plan.")]
    [InlineData("third_party", null)]
    [InlineData(null, null)]
    public async Task The_usage_note_follows_how_claude_code_says_it_is_signed_in_and_is_absent_when_that_is_unknown(string? authMethod, string? note)
    {
        using var world = new World(new { signedIn = true, authMethod, adapt = new { kind = "success", skillFile = AdaptedSkill } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.WaitForSelectorAsync(".adapt-provider-line");
        await Task.Delay(500);

        Assert.Equal("· Connected via Claude Code", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        if (note is null)
        {
            Assert.Equal(0, await page.Locator(".adapt-provider-note").CountAsync());
        }
        else
        {
            Assert.Equal(note, (await page.Locator(".adapt-provider-note").TextContentAsync())!.Trim());
        }
    }

    [Fact]
    public async Task A_test_that_gives_no_claude_never_reaches_the_real_one_even_when_one_is_installed()
    {
        using var world = new World(Success);
        await using var app = await world.StartWithoutClaudeAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        Assert.Equal("Claude Code isn't installed.", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
        Assert.False(File.Exists(DesktopApp.NoClaude));
        Assert.Empty(world.Fake.Calls());
    }

    [Fact]
    public async Task Without_claude_code_the_dialog_says_so_and_how_to_install_opens_the_install_page()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync(claudePath: Path.Combine(Path.GetTempPath(), "scion-no-such-claude.exe"));
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await app.ScreenshotAsync("adapt-not-installed");
        Assert.Equal("Claude Code isn't installed.", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
        await page.Locator(".provider-action").ClickAsync();

        await Eventually(() => app.ReadShellLog().Contains("open https://code.claude.com/docs/en/setup"));
        Assert.Empty(world.Fake.Calls());
    }

    [Fact]
    public async Task Signed_out_the_user_signs_in_waits_for_the_browser_and_then_can_adapt()
    {
        using var world = new World(new { signedIn = false, loginDelayMs = 1500, adapt = new { kind = "success", skillFile = AdaptedSkill } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await app.ScreenshotAsync("adapt-sign-in");
        Assert.Equal("Sign in to Claude Code to continue.", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        await page.Locator(".provider-action").ClickAsync();
        await page.WaitForSelectorAsync(".adapt-provider .text-action");
        await app.ScreenshotAsync("adapt-waiting");
        Assert.Equal("Finish signing in in your browser…", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());

        await page.WaitForSelectorAsync(".adapt-provider .text-action", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached, Timeout = 15_000 });

        Assert.Equal("· Connected via Claude Code", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.False(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
    }

    [Fact]
    public async Task A_usage_limit_shows_the_amber_line_and_Try_again_runs_it_once_more()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "limitByStatus" } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-footer .field-problem");
        await app.ScreenshotAsync("adapt-error");

        Assert.Equal(
            "Claude Code couldn't finish: your usage limit was reached. Try again later.",
            (await page.Locator(".dialog-footer .field-problem").TextContentAsync())!.Trim());
        world.Fake.Write(Success);
        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Try again" }).ClickAsync();
        await page.Locator(".dialog-footer .button-primary").ClickAsync();

        await page.WaitForSelectorAsync(".dialog-adapt-review");
        Assert.Equal(2, world.Fake.SentPrompts().Length);
    }

    [Fact]
    public async Task Cancelling_while_it_runs_ends_the_claude_process()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "hang" } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".adapt-running");
        await Eventually(() => world.Fake.IsRunning());
        await app.ScreenshotAsync("adapt-running");
        await page.Locator(".dialog-footer .button").ClickAsync();

        await Eventually(() => !world.Fake.IsRunning());
        await page.WaitForSelectorAsync(".adapt-running", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.False(world.Fake.IsRunning());
    }

    [Fact]
    public async Task In_a_small_window_the_title_and_the_buttons_stay_and_only_the_body_scrolls()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator("textarea.adapt-instructions").FillAsync(string.Join(" ", Enumerable.Repeat("A long extra instruction.", 12)));
        await page.Locator(".shared-toggle").ClickAsync();
        await page.SetViewportSizeAsync(900, 460);

        await app.ScreenshotAsync("adapt-dialog-small");
        var dialog = (await page.Locator(".dialog-adapt").BoundingBoxAsync())!;
        var header = (await page.Locator(".dialog-adapt .dialog-header h2").BoundingBoxAsync())!;
        var adapt = await SettledBoxAsync(page.Locator(".dialog-footer .button-primary"));

        Assert.True(dialog.Y >= 0 && dialog.Y + dialog.Height <= 460, $"The dialog ({dialog.Y}, {dialog.Height}) does not fit in the window.");
        Assert.True(header.Y >= 0 && adapt.Y + adapt.Height <= 460);
        Assert.True(await page.Locator(".dialog-body").EvaluateAsync<bool>("body => body.scrollHeight > body.clientHeight"));

        await page.Locator(".dialog-body").EvaluateAsync("body => { body.scrollTop = body.scrollHeight; }");

        var after = await SettledBoxAsync(page.Locator(".dialog-footer .button-primary"));
        Assert.Equal(adapt.Y, after.Y);
        Assert.True(await page.Locator(".dialog-adapt .dialog-header h2").IsVisibleAsync());
    }

    [Fact]
    public async Task The_summary_has_the_files_of_a_project_in_one_line_and_the_footer_has_two_lines_of_provider_text()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        Assert.Equal(
            ["SKILL.md from compose", "2 project files", "Folder names in web-b (two levels)"],
            (await page.Locator(".shared-list li").AllTextContentsAsync()).Select(text => Regex.Replace(text.Trim(), @" · \d+ KB$", "")).ToArray());
        Assert.Equal("Show all", (await page.Locator(".shared-toggle").TextContentAsync())!.Trim());
        await page.WaitForSelectorAsync(".adapt-provider-note");
        Assert.Equal(2, await page.Locator(".adapt-provider-text > *").CountAsync());
    }

    [Fact]
    public async Task While_it_runs_the_text_appears_piece_by_piece_with_the_time_so_far_and_then_the_review_opens()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, chunkSize = 6, chunkDelayMs = 150 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForFunctionAsync("() => (document.querySelector('.streaming-text')?.textContent ?? '').includes('description')");

        await app.ScreenshotAsync("adapt-streaming");
        var partial = await OwnText(page);
        Assert.NotEmpty(partial);
        Assert.True(partial.Length < AdaptedSkill.Length);
        Assert.Matches(@"^\d+:\d{2}$", (await page.Locator(".adapt-elapsed").TextContentAsync())!);
        Assert.Equal(["Cancel"], (await page.Locator(".dialog-footer .button").AllTextContentsAsync()).Select(text => text.Trim()).ToArray());

        await page.WaitForSelectorAsync(".dialog-adapt-review");
        Assert.Equal(0, await page.Locator(".streaming-text").CountAsync());
    }

    [Fact]
    public async Task Before_the_first_text_only_the_sparkle_shows_in_the_middle_and_then_it_is_the_cursor_after_the_text()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, firstChunkDelayMs = 3000, chunkSize = 6, chunkDelayMs = 200 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".streaming-empty .sparkle");

        await app.ScreenshotAsync("adapt-streaming-waiting");
        var box = (await page.Locator(".streaming-text").BoundingBoxAsync())!;
        var sparkle = (await page.Locator(".streaming-text .sparkle").BoundingBoxAsync())!;
        Assert.Equal("", await OwnText(page));
        Assert.Equal(0, await page.Locator(".streaming-text img, .streaming-text text, .streaming-text image").CountAsync());
        Assert.InRange(sparkle.X + sparkle.Width / 2, box.X + box.Width / 2 - 2, box.X + box.Width / 2 + 2);
        Assert.InRange(sparkle.Y + sparkle.Height / 2, box.Y + box.Height / 2 - 2, box.Y + box.Height / 2 + 2);
        Assert.Equal("rgb(156, 211, 175)", await page.Locator(".streaming-text .sparkle").EvaluateAsync<string>("svg => getComputedStyle(svg).color"));

        var frames = new HashSet<int>();
        for (var sample = 0; sample < 40; sample++)
        {
            frames.Add(await VisibleFrame(page));
            await Task.Delay(35);
        }

        Assert.True(frames.Count >= 4, $"The sparkle showed only the frames {string.Join(", ", frames.Order())}.");
        Assert.DoesNotContain(-1, frames);

        await page.WaitForFunctionAsync("() => (document.querySelector('.streaming-text')?.textContent ?? '').includes('name')");
        await app.ScreenshotAsync("adapt-streaming-cursor");
        Assert.Equal(0, await page.Locator(".streaming-empty").CountAsync());
        Assert.Equal(1, await page.Locator(".streaming-text .sparkle").CountAsync());
        Assert.Equal(1, await page.Locator(".streaming-text > .streaming-cursor > .sparkle").CountAsync());
        Assert.NotEqual("", await OwnText(page));
    }

    [Fact]
    public async Task The_six_frames_of_the_sparkle_are_six_different_shapes()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, firstChunkDelayMs = 6000 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".streaming-empty .sparkle");
        await page.EvaluateAsync("() => document.querySelectorAll('.sparkle-frame').forEach(frame => { frame.style.animation = 'none'; })");

        var shapes = new List<byte[]>();
        for (var frame = 0; frame < 6; frame++)
        {
            await page.EvaluateAsync(
                "shown => document.querySelectorAll('.streaming-text .sparkle-frame').forEach((frame, index) => { frame.style.opacity = index === shown ? '1' : '0'; })",
                frame);
            shapes.Add(await page.Locator(".streaming-text .sparkle").ScreenshotAsync());
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        for (var frame = 0; frame < 6; frame++)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, $"adapt-sparkle-frame-{frame}.png"), shapes[frame]);
        }

        Assert.Equal(6, shapes.Select(Convert.ToBase64String).Distinct().Count());
    }

    [Fact]
    public async Task With_reduced_motion_the_sparkle_is_one_still_shape()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, firstChunkDelayMs = 3000, chunkSize = 6 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".streaming-empty .sparkle");

        var frames = new HashSet<int>();
        for (var sample = 0; sample < 12; sample++)
        {
            frames.Add(await VisibleFrame(page));
            await Task.Delay(100);
        }

        Assert.Equal([4], frames);
        Assert.Equal("none", await page.Locator(".sparkle-frame-4").EvaluateAsync<string>("frame => getComputedStyle(frame).animationName"));
    }

    [Fact]
    public async Task Cancelling_in_the_middle_of_the_stream_ends_the_process_and_writes_nothing()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, chunkSize = 6, hangAfterChunks = 8 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForFunctionAsync("() => (document.querySelector('.streaming-text')?.textContent ?? '').length > 0");

        await page.Locator(".dialog-footer .button").ClickAsync();

        await Eventually(() => !world.Fake.IsRunning());
        await page.WaitForSelectorAsync(".adapt-running", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".claude")));
    }

    [Fact]
    public async Task A_stream_that_stops_early_is_an_error_and_writes_nothing()
    {
        using var world = new World(new { signedIn = true, adapt = new { kind = "success", skillFile = AdaptedSkill, chunkSize = 6, stopAfterChunks = 5 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-footer .field-problem");

        Assert.StartsWith("Claude Code couldn't finish:", (await page.Locator(".dialog-footer .field-problem").TextContentAsync())!.Trim());
        Assert.Equal(0, await page.Locator(".dialog-adapt-review").CountAsync());
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".claude")));
    }

    [Fact]
    public async Task Edit_before_saving_saves_what_the_user_wrote()
    {
        using var world = new World(Success);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");

        await page.Locator(".dialog-footer .button", new PageLocatorOptions { HasText = "Edit before saving" }).ClickAsync();
        await page.Locator("textarea.text-editor-input").FillAsync("---\nname: compose\ndescription: Mine.\n---\nMy own text.\n");
        await app.ScreenshotAsync("adapt-edit");
        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        Assert.Equal(
            "---\nname: compose\ndescription: Mine.\n---\nMy own text.\n",
            await File.ReadAllTextAsync(Path.Combine(world.TargetProject, ".claude", "skills", "compose", "SKILL.md")));
    }

    /// <summary>The index of the frame the sparkle shows right now, -1 when none or several are visible.</summary>
    private static async Task<int> VisibleFrame(IPage page) =>
        await page.EvaluateAsync<int>(
            @"() => { const shown = [...document.querySelectorAll('.streaming-text .sparkle-frame')].map((frame, index) => getComputedStyle(frame).opacity === '1' ? index : -1).filter(index => index >= 0); return shown.length === 1 ? shown[0] : -1; }");

    /// <summary>The text of the box itself, without what is drawn inside it.</summary>
    private static async Task<string> OwnText(IPage page) =>
        await page.Locator(".streaming-text").EvaluateAsync<string>(
            "box => [...box.childNodes].filter(node => node.nodeType === 3).map(node => node.data).join('')");

    /// <summary>The box of an element once the layout has stopped moving (a resize or a scroll may still be settling).</summary>
    private static async Task<LocatorBoundingBoxResult> SettledBoxAsync(ILocator locator)
    {
        var previous = (await locator.BoundingBoxAsync())!;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(100);
            var current = (await locator.BoundingBoxAsync())!;
            if (current.X == previous.X && current.Y == previous.Y && current.Width == previous.Width && current.Height == previous.Height)
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }

    /// <summary>Two projects: "api-a" has the skill "compose" with a script, "web-b" has a CLAUDE.md, a manifest and a source file.</summary>
    private sealed class World : IDisposable
    {
        private readonly TempDirectory _root = new("scion-adapt");

        public World(object script)
        {
            Fake = new FakeClaudeRun(script);
            SourceProject = Path.Combine(_root.Path, "api-a");
            TargetProject = Path.Combine(_root.Path, "web-b");

            var skill = Path.Combine(SourceProject, ".claude", "skills", "compose");
            Directory.CreateDirectory(Path.Combine(skill, "scripts"));
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), SourceSkill);
            File.WriteAllBytes(Path.Combine(skill, "scripts", "run.bin"), ScriptBytes);

            Directory.CreateDirectory(Path.Combine(TargetProject, "src", "Web"));
            File.WriteAllText(Path.Combine(TargetProject, "CLAUDE.md"), "# web-b\nUse PostgreSQL.\n");
            File.WriteAllText(Path.Combine(TargetProject, "web-b.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />\n");
            File.WriteAllText(Path.Combine(TargetProject, "Program.cs"), "// SECRET source\n");
            File.WriteAllText(Path.Combine(TargetProject, "src", "Web", "Controller.cs"), "// SECRET source\n");
        }

        public FakeClaudeRun Fake { get; }

        public string SourceProject { get; }

        public string TargetProject { get; }

        public Task<DesktopApp> StartAsync(string? claudePath = null)
        {
            var scionHome = new TempDirectory("scion-home");
            Fixtures.WriteRegistry(scionHome, (SourceProject, "api-a"), (TargetProject, "web-b"));

            return DesktopApp.StartAsync(
                scionHome, recordShell: true, claudePath: claudePath ?? Fake.ExecutablePath, fakeClaudeScript: Fake.ScriptPath);
        }

        /// <summary>The app as a test starts it when it names no claude at all.</summary>
        public Task<DesktopApp> StartWithoutClaudeAsync()
        {
            var scionHome = new TempDirectory("scion-home");
            Fixtures.WriteRegistry(scionHome, (SourceProject, "api-a"), (TargetProject, "web-b"));

            return DesktopApp.StartAsync(scionHome, recordShell: true, fakeClaudeScript: Fake.ScriptPath);
        }

        public async Task OpenSourceAsync(DesktopApp app)
        {
            var page = app.Page;
            await page.WaitForSelectorAsync(".project-card");
            await page.Locator(".project-card", new PageLocatorOptions { HasText = "api-a" }).Locator(".project-open").ClickAsync();
            await page.WaitForSelectorAsync(".skill-row");
        }

        public async Task RowMenuAsync(DesktopApp app) => await app.Page.Locator(".skill-row .options-button").ClickAsync();

        /// <summary>Opens the dialog from the row menu and chooses web-b as the target.</summary>
        public async Task OpenAdaptDialogAsync(DesktopApp app)
        {
            await OpenSourceAsync(app);
            await RowMenuAsync(app);
            await app.Page.Locator("[role=menuitem]", new PageLocatorOptions { HasText = "Adapt with AI" }).ClickAsync();
            await app.Page.WaitForSelectorAsync("[role=dialog]");
            await app.Page.Locator(".place-picker-button").ClickAsync();
            await app.Page.Locator(".place-option", new PageLocatorOptions { HasText = "web-b" }).ClickAsync();
            await app.Page.WaitForSelectorAsync(".shared-list li");
        }

        public void Dispose()
        {
            Fake.Dispose();
            _root.Dispose();
        }
    }
}
