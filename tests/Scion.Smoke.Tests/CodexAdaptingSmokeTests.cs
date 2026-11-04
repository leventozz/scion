using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Adapting a skill with Codex, and choosing between Claude and Codex, against the real desktop app and the real file system,
/// with stand-ins for <c>claude</c> and <c>codex</c>. Who adapts is chosen apart from where the skill goes: the agent of the target
/// stays what the user set. The screens of the provider picker designs are screenshotted for a visual check.
/// </summary>
[Trait("Category", "Smoke")]
public class CodexAdaptingSmokeTests
{
    private const string SourceSkill = "---\nname: compose\ndescription: Checks compose files.\n---\n# Compose\nUses MongoDB.\n";
    private const string AdaptedSkill = "---\nname: renamed-by-the-model\ndescription: Checks compose files.\n---\n# Compose\nUses PostgreSQL.\n";
    private const string PersonalRulesMarker = "PERSONAL-RULES-MARKER";
    private const string CodexNote = "Counts toward your ChatGPT plan unless an API key is set for Codex.";

    private static object CodexSuccess => new { signedIn = true, exec = new { kind = "success", skillFile = AdaptedSkill } };

    [Fact]
    public async Task The_picker_works_with_the_keyboard_and_Escape_only_closes_its_list()
    {
        using var world = new World(CodexSuccess);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        Assert.Equal("Claude▾", await TriggerText(page));

        await page.Locator(".provider-picker-trigger").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForSelectorAsync("[role=listbox]");
        await page.WaitForFunctionAsync("() => document.activeElement?.getAttribute('role') === 'listbox'");
        await app.ScreenshotAsync("adapt-provider-picker");

        Assert.Equal("true", await page.Locator(".provider-picker-trigger").GetAttributeAsync("aria-expanded"));
        Assert.Equal(["Claude", "Codex"], (await page.Locator("[role=option] .provider-option-name").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal(
            ["Connected via Claude Code", "Connected via Codex CLI"],
            (await page.Locator("[role=option] .provider-option-state").AllTextContentsAsync()).Select(text => text.Trim()));
        Assert.Equal("true", await page.Locator("[role=option]").First.GetAttributeAsync("aria-selected"));
        Assert.Equal(1, await page.Locator("[role=option] svg.provider-option-check").CountAsync());

        await page.Keyboard.PressAsync("Escape");

        await page.WaitForSelectorAsync("[role=listbox]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.Equal(1, await page.Locator("[role=dialog]").CountAsync());
        await page.WaitForFunctionAsync("() => document.activeElement?.classList.contains('provider-picker-trigger') === true");

        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForFunctionAsync("() => document.activeElement?.getAttribute('role') === 'listbox'");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForFunctionAsync("() => document.querySelector('.provider-picker-trigger')?.textContent.trim() === 'Codex▾'");
        await page.WaitForFunctionAsync("() => document.activeElement?.classList.contains('provider-picker-trigger') === true");
        Assert.Equal(1, await page.Locator("[role=dialog]").CountAsync());
    }

    [Fact]
    public async Task The_trigger_lists_connected_providers_as_the_design_does_and_the_list_opens_upward()
    {
        using var world = new World(CodexSuccess);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".provider-picker-trigger").ClickAsync();
        await page.WaitForSelectorAsync("[role=listbox]");

        var trigger = (await page.Locator(".provider-picker-trigger").BoundingBoxAsync())!;
        var list = (await page.Locator("[role=listbox]").BoundingBoxAsync())!;
        Assert.True(list.Y + list.Height <= trigger.Y, "The list must open above the trigger.");
    }

    [Fact]
    public async Task What_the_dialog_lists_is_what_codex_receives_apart_from_the_file_codex_loads_by_itself()
    {
        using var world = new World(CodexSuccess);
        world.WriteCodexFile("AGENTS.md", $"{PersonalRulesMarker} " + new string('a', 1200));
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await page.Locator("textarea.adapt-instructions").FillAsync("We use PostgreSQL.");

        await ChooseCodexAsync(page);
        await page.WaitForFunctionAsync("() => [...document.querySelectorAll('.shared-list li')].some(item => item.textContent.includes('loaded by Codex'))");

        await app.ScreenshotAsync("adapt-codex-ready");
        Assert.Equal("· Connected via Codex CLI", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.Equal(CodexNote, (await page.Locator(".adapt-provider-note").TextContentAsync())!.Trim());
        Assert.Equal(
            "~/.codex/AGENTS.md (loaded by Codex) · 2 KB",
            (await page.Locator(".shared-list li").Last.TextContentAsync())!.Trim());
        await page.Locator(".shared-toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.shared-list li').length === 6");
        var shown = (await page.Locator(".shared-list li").AllTextContentsAsync()).Select(text => text.Trim()).ToArray();
        Assert.Equal("~/.codex/AGENTS.md (loaded by Codex) · 2 KB", shown[^1]);
        var listed = shown[..^1].Select(text => Regex.Replace(text, @" · \d+ KB$", "")).ToArray();

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");

        var prompt = Assert.Single(world.Codex.SentPrompts());
        var titles = Regex.Matches(prompt, @"^<<< (.+) >>>$", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(listed, titles);
        Assert.Contains("We use PostgreSQL.", prompt);
        Assert.DoesNotContain(PersonalRulesMarker, prompt);
        Assert.DoesNotContain("loaded by", prompt);
        Assert.DoesNotContain("SECRET", prompt);
        Assert.Empty(world.Claude.SentPrompts());
    }

    [Fact]
    public async Task When_an_override_file_exists_the_loaded_line_shows_that_file_because_codex_loads_that_one()
    {
        using var world = new World(CodexSuccess);
        world.WriteCodexFile("AGENTS.md", "Personal rules.");
        world.WriteCodexFile("AGENTS.override.md", "Override rules.");
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await ChooseCodexAsync(page);
        await page.WaitForFunctionAsync("() => [...document.querySelectorAll('.shared-list li')].some(item => item.textContent.includes('loaded by Codex'))");

        Assert.Equal(
            "~/.codex/AGENTS.override.md (loaded by Codex) · 1 KB",
            (await page.Locator(".shared-list li").Last.TextContentAsync())!.Trim());
    }

    [Fact]
    public async Task Codex_is_started_with_the_flags_that_keep_it_to_the_prompt()
    {
        using var world = new World(CodexSuccess);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await ChooseCodexAsync(page);

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");

        var call = Assert.Single(world.Codex.Calls(), call => call.GetProperty("args")[0].GetString() == "exec");
        var args = call.GetProperty("args").EnumerateArray().Select(argument => argument.GetString()!).ToArray();
        Assert.Contains("--ignore-user-config", args);
        Assert.Contains("--ignore-rules", args);
        Assert.Contains("--ephemeral", args);
        Assert.Equal("read-only", args[Array.IndexOf(args, "--sandbox") + 1]);
        Assert.Equal(0, call.GetProperty("cwdEntries").GetInt32());
    }

    [Fact]
    public async Task Signed_out_codex_signs_in_with_ChatGPT_and_the_other_provider_is_not_asked_to_sign_in()
    {
        using var world = new World(new { signedIn = false, loginDelayMs = 1500, exec = new { kind = "success", skillFile = AdaptedSkill } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await ChooseCodexAsync(page);
        await page.WaitForSelectorAsync(".provider-action");

        await app.ScreenshotAsync("adapt-codex-sign-in");
        Assert.Equal("Codex▾", await TriggerText(page));
        Assert.Equal("Sign in to Codex CLI to continue.", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.Equal("Sign in with ChatGPT", (await page.Locator(".provider-action").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
        await page.Locator(".provider-action").ClickAsync();
        await page.WaitForSelectorAsync(".adapt-provider .text-action");

        Assert.True(await page.Locator(".provider-picker-trigger").IsDisabledAsync());
        await page.WaitForSelectorAsync(".adapt-provider .text-action", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached, Timeout = 15_000 });

        Assert.Equal("· Connected via Codex CLI", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.False(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
        Assert.Contains(world.Codex.Calls(), call => call.GetProperty("args").GetArrayLength() == 1 && call.GetProperty("args")[0].GetString() == "login");
        Assert.DoesNotContain(world.Claude.Calls(), call => call.GetProperty("args").EnumerateArray().Any(argument => argument.GetString() == "login"));
    }

    [Fact]
    public async Task Without_codex_the_row_says_so_and_how_to_install_opens_the_install_page()
    {
        using var world = new World(CodexSuccess);
        await using var app = await world.StartAsync(withCodex: false);
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;

        await page.Locator(".provider-picker-trigger").ClickAsync();
        await page.WaitForSelectorAsync("[role=listbox]");
        Assert.Equal("Not installed", (await page.Locator("[role=option] .provider-option-state").Last.TextContentAsync())!.Trim());
        await page.Locator("[role=option]").Last.ClickAsync();
        await page.WaitForSelectorAsync(".provider-action");

        Assert.Equal("Codex CLI isn't installed.", (await page.Locator(".adapt-provider-line").TextContentAsync())!.Trim());
        Assert.True(await page.Locator(".dialog-footer .button-primary").IsDisabledAsync());
        await page.Locator(".provider-action").ClickAsync();

        await Eventually(() => app.ReadShellLog().Contains("open https://developers.openai.com/codex/cli"));
        Assert.Empty(world.Codex.Calls());
        Assert.False(File.Exists(DesktopApp.NoCodex));
    }

    [Fact]
    public async Task A_tool_that_ran_shows_the_amber_error_writes_nothing_and_ends_the_process()
    {
        using var world = new World(new { signedIn = true, exec = new { kind = "toolRan" } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await ChooseCodexAsync(page);

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-footer .field-problem");

        Assert.Equal(
            "Codex CLI couldn't finish: Codex used a tool, so Scion stopped it and discarded the result.",
            (await page.Locator(".dialog-footer .field-problem").TextContentAsync())!.Trim());
        Assert.Equal(0, await page.Locator(".dialog-adapt-review").CountAsync());
        await Eventually(() => !world.Codex.IsRunning());
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".claude")));
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".agents")));
    }

    [Fact]
    public async Task A_tool_that_was_blocked_is_no_problem_and_the_review_saves_to_the_target_agent_the_user_set()
    {
        using var world = new World(new { signedIn = true, exec = new { kind = "toolBlocked", skillFile = AdaptedSkill } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await ChooseCodexAsync(page);
        Assert.Equal(["Claude"], await PressedAgent(page));

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".dialog-adapt-review");
        await page.Locator(".dialog-footer .button-primary", new PageLocatorOptions { HasText = "Save to web-b" }).ClickAsync();
        await page.WaitForSelectorAsync("[role=dialog]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        var skill = await File.ReadAllTextAsync(Path.Combine(world.TargetProject, ".claude", "skills", "compose", "SKILL.md"));
        Assert.Equal(AdaptedSkill.Replace("renamed-by-the-model", "compose"), skill);
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".agents")));
    }

    [Fact]
    public async Task Choosing_the_provider_leaves_the_target_agent_and_the_list_of_what_is_shared_as_they_are()
    {
        using var world = new World(CodexSuccess);
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        var shared = await page.Locator(".shared-list li").AllTextContentsAsync();

        await ChooseCodexAsync(page);

        Assert.Equal(["Claude"], await PressedAgent(page));
        Assert.Equal(shared, await page.Locator(".shared-list li").AllTextContentsAsync());
        Assert.Contains("web-b", (await page.Locator(".place-picker-button").TextContentAsync())!);
    }

    [Fact]
    public async Task Cancelling_while_codex_runs_ends_the_codex_process()
    {
        using var world = new World(new { signedIn = true, exec = new { kind = "hang" } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await ChooseCodexAsync(page);

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".adapt-running");
        await Eventually(() => world.Codex.IsRunning());
        await page.Locator(".dialog-footer .button").ClickAsync();

        await Eventually(() => !world.Codex.IsRunning());
        await page.WaitForSelectorAsync(".adapt-running", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
        Assert.False(Directory.Exists(Path.Combine(world.TargetProject, ".claude")));
    }

    [Fact]
    public async Task While_codex_works_only_the_sparkle_shows_because_it_does_not_stream_its_answer()
    {
        using var world = new World(new { signedIn = true, exec = new { kind = "success", skillFile = AdaptedSkill, delayMs = 2500 } });
        await using var app = await world.StartAsync();
        await world.OpenAdaptDialogAsync(app);
        var page = app.Page;
        await ChooseCodexAsync(page);

        await page.Locator(".dialog-footer .button-primary").ClickAsync();
        await page.WaitForSelectorAsync(".streaming-empty .sparkle");
        await Task.Delay(1200);

        Assert.Equal(1, await page.Locator(".streaming-empty .sparkle").CountAsync());
        await page.WaitForSelectorAsync(".dialog-adapt-review");
    }

    [Fact]
    public async Task The_provider_used_last_is_chosen_the_next_time_and_noted_in_scions_own_folder()
    {
        using var world = new World(CodexSuccess);
        var note = Path.Combine(world.ScionHome.Path, ".scion", "ai-provider.json");
        using var nextHome = new TempDirectory("scion-home");

        await using (var first = await world.StartAsync())
        {
            await world.OpenAdaptDialogAsync(first);
            await ChooseCodexAsync(first.Page);
            Assert.False(File.Exists(note), "Only pressing Adapt notes the provider.");
            await first.Page.Locator(".dialog-footer .button-primary").ClickAsync();
            await first.Page.WaitForSelectorAsync(".dialog-adapt-review");

            Assert.Contains("\"lastUsed\": \"codex\"", await File.ReadAllTextAsync(note));
            Directory.CreateDirectory(Path.Combine(nextHome.Path, ".scion"));
            File.Copy(note, Path.Combine(nextHome.Path, ".scion", "ai-provider.json"));
            Fixtures.WriteRegistry(nextHome, (world.SourceProject, "api-a"), (world.TargetProject, "web-b"));
        }

        await using var second = await DesktopApp.StartAsync(
            nextHome,
            recordShell: true,
            claudePath: world.Claude.ExecutablePath,
            fakeClaudeScript: world.Claude.ScriptPath,
            codexPath: world.Codex.ExecutablePath,
            fakeCodexScript: world.Codex.ScriptPath);
        await world.OpenAdaptDialogAsync(second);

        Assert.Equal("Codex▾", await TriggerText(second.Page));
        Assert.Equal(CodexNote, (await second.Page.Locator(".adapt-provider-note").TextContentAsync())!.Trim());
    }

    [Fact]
    public async Task Without_a_note_the_first_ready_provider_is_chosen_and_a_not_ready_one_used_last_is_still_chosen()
    {
        using var world = new World(new { signedIn = false });
        await using (var app = await world.StartAsync())
        {
            await world.OpenAdaptDialogAsync(app);

            Assert.Equal("Claude▾", await TriggerText(app.Page));
        }

        using var nextHome = new TempDirectory("scion-home");
        Directory.CreateDirectory(Path.Combine(nextHome.Path, ".scion"));
        File.WriteAllText(Path.Combine(nextHome.Path, ".scion", "ai-provider.json"), "{ \"lastUsed\": \"codex\" }");
        Fixtures.WriteRegistry(nextHome, (world.SourceProject, "api-a"), (world.TargetProject, "web-b"));
        await using var second = await DesktopApp.StartAsync(
            nextHome,
            recordShell: true,
            claudePath: world.Claude.ExecutablePath,
            fakeClaudeScript: world.Claude.ScriptPath,
            codexPath: world.Codex.ExecutablePath,
            fakeCodexScript: world.Codex.ScriptPath);
        await world.OpenAdaptDialogAsync(second);

        Assert.Equal("Codex▾", await TriggerText(second.Page));
        Assert.Equal("Sign in with ChatGPT", (await second.Page.Locator(".provider-action").TextContentAsync())!.Trim());
    }

    private static async Task ChooseCodexAsync(IPage page)
    {
        await page.Locator(".provider-picker-trigger").ClickAsync();
        await page.Locator("[role=option]", new PageLocatorOptions { HasText = "Codex" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.provider-picker-trigger')?.textContent.trim() === 'Codex▾'");
        await page.WaitForSelectorAsync("[role=listbox]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
    }

    private static async Task<string> TriggerText(IPage page) => (await page.Locator(".provider-picker-trigger").TextContentAsync())!.Trim();

    private static async Task<string[]> PressedAgent(IPage page) =>
        [.. (await page.Locator("[role=dialog] .agent-switch button[aria-pressed=true]").AllTextContentsAsync()).Select(text => text.Trim())];

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }

    /// <summary>Two projects: "api-a" has the skill "compose" for Claude, "web-b" has a CLAUDE.md, a manifest and a source file.</summary>
    private sealed class World : IDisposable
    {
        private readonly TempDirectory _root = new("scion-codex-adapt");

        public World(object codexScript)
        {
            Codex = new FakeCodexRun(codexScript);
            Claude = new FakeClaudeRun(new { signedIn = true, authMethod = "claude.ai", adapt = new { kind = "success", skillFile = AdaptedSkill } });
            ScionHome = new TempDirectory("scion-home");
            SourceProject = Path.Combine(_root.Path, "api-a");
            TargetProject = Path.Combine(_root.Path, "web-b");

            var skill = Path.Combine(SourceProject, ".claude", "skills", "compose");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), SourceSkill);

            Directory.CreateDirectory(Path.Combine(TargetProject, "src", "Web"));
            File.WriteAllText(Path.Combine(TargetProject, "CLAUDE.md"), "# web-b\nUse PostgreSQL.\n");
            File.WriteAllText(Path.Combine(TargetProject, "web-b.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />\n");
            File.WriteAllText(Path.Combine(TargetProject, "Program.cs"), "// SECRET source\n");
            File.WriteAllText(Path.Combine(TargetProject, "src", "Web", "Controller.cs"), "// SECRET source\n");
        }

        public FakeCodexRun Codex { get; }

        public FakeClaudeRun Claude { get; }

        public TempDirectory ScionHome { get; }

        public string SourceProject { get; }

        public string TargetProject { get; }

        /// <summary>A file in the personal folder of Codex, under the root the app uses for Personal (SCION_HOME).</summary>
        public void WriteCodexFile(string name, string content)
        {
            var folder = Path.Combine(ScionHome.Path, ".codex");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), content);
        }

        /// <param name="withCodex">False starts the app as a test does when it names no codex at all: it is never found by searching.</param>
        public Task<DesktopApp> StartAsync(bool withCodex = true)
        {
            Fixtures.WriteRegistry(ScionHome, (SourceProject, "api-a"), (TargetProject, "web-b"));

            return DesktopApp.StartAsync(
                ScionHome,
                recordShell: true,
                claudePath: Claude.ExecutablePath,
                fakeClaudeScript: Claude.ScriptPath,
                codexPath: withCodex ? Codex.ExecutablePath : null,
                fakeCodexScript: Codex.ScriptPath);
        }

        public async Task OpenAdaptDialogAsync(DesktopApp app)
        {
            var page = app.Page;
            await page.WaitForSelectorAsync(".project-card");
            await page.Locator(".project-card", new PageLocatorOptions { HasText = "api-a" }).Locator(".project-open").ClickAsync();
            await page.WaitForSelectorAsync(".skill-row");
            await page.Locator(".skill-row .options-button").ClickAsync();
            await page.Locator("[role=menuitem]", new PageLocatorOptions { HasText = "Adapt with AI" }).ClickAsync();
            await page.WaitForSelectorAsync("[role=dialog]");
            await page.WaitForSelectorAsync(".provider-picker-trigger");
            await page.Locator(".place-picker-button").ClickAsync();
            await page.Locator(".place-option", new PageLocatorOptions { HasText = "web-b" }).ClickAsync();
            await page.WaitForSelectorAsync(".shared-list li");
        }

        public void Dispose()
        {
            Codex.Dispose();
            Claude.Dispose();
            ScionHome.Dispose();
            _root.Dispose();
        }
    }
}
