using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Microsoft.Playwright;

namespace Scion.Smoke.Tests;

/// <summary>
/// Starts the real desktop app with its own SCION_HOME and drives its WebView2 over the Chrome DevTools Protocol.
/// </summary>
internal sealed class DesktopApp : IAsyncDisposable
{
    private readonly Process _process;
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;

    private DesktopApp(Process process, IPlaywright playwright, IBrowser browser, IPage page, TempDirectory scionHome)
    {
        _process = process;
        _playwright = playwright;
        _browser = browser;
        Page = page;
        ScionHome = scionHome;
    }

    public IPage Page { get; }

    public TempDirectory ScionHome { get; }

    public int ProcessId => _process.Id;

    public bool HasExited => _process.HasExited;

    /// <summary>Does what the close button does (Windows only); the app may keep the window open.</summary>
    [SupportedOSPlatform("windows")]
    public void RequestWindowClose() => NativeWindows.RequestClose(_process.Id, "Scion");

    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            await _process.WaitForExitAsync(cancellation.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public string ShellLog => Path.Combine(ScionHome.Path, "shell.log");

    public string RegistryFile => Path.Combine(ScionHome.Path, ".scion", "projects.json");

    /// <param name="scionHome">Holds the registry. Created empty when not given.</param>
    /// <param name="testFolder">When set, "Add project" picks this folder instead of opening the native dialog.</param>
    /// <param name="recordShell">
    /// When true, "Show in folder" and web links are written to <see cref="ShellLog"/> instead of opening
    /// the file manager or the browser.
    /// </param>
    /// <param name="claudePath">
    /// The program the app runs as <c>claude</c> (SCION_CLAUDE_PATH). Without one it is <see cref="NoClaude"/>, a file that
    /// does not exist, so no test can reach the user's real Claude Code, not even through the search path.
    /// </param>
    /// <param name="fakeClaudeScript">The script the stand-in claude reads (SCION_FAKE_CLAUDE_SCRIPT).</param>
    /// <param name="codexPath">
    /// The program the app runs as <c>codex</c> (SCION_CODEX_PATH). Without one it is <see cref="NoCodex"/>, a file that
    /// does not exist, so no test can reach the user's real Codex, not even through the search path.
    /// </param>
    /// <param name="fakeCodexScript">The script the stand-in codex reads (SCION_FAKE_CODEX_SCRIPT).</param>
    /// <param name="updateSource">
    /// The folder of releases the app updates from (SCION_UPDATE_SOURCE). Without one it is <see cref="NoUpdateSource"/>, a folder
    /// that does not exist, so no test can reach GitHub, not even through the default address.
    /// </param>
    /// <param name="installedVersion">
    /// When set, the app behaves as if this version was installed (SCION_TEST_INSTALLED_VERSION). Without it the app is not
    /// installed, so it looks for no update at all.
    /// </param>
    /// <summary>A path that never exists. An explicit path is the only place the app looks, so it never falls back to the real claude.</summary>
    public static readonly string NoClaude = Path.Combine(Path.GetTempPath(), "scion-tests-no-claude", "claude.exe");

    /// <summary>A path that never exists, for the same reason as <see cref="NoClaude"/>.</summary>
    public static readonly string NoCodex = Path.Combine(Path.GetTempPath(), "scion-tests-no-codex", "codex.exe");

    /// <summary>A folder that never exists, for the same reason as <see cref="NoClaude"/>: the update source is never the default address.</summary>
    public static readonly string NoUpdateSource = Path.Combine(Path.GetTempPath(), "scion-tests-no-update-source");

    public static async Task<DesktopApp> StartAsync(
        TempDirectory? scionHome = null,
        string? testFolder = null,
        bool recordShell = false,
        string? claudePath = null,
        string? fakeClaudeScript = null,
        string? codexPath = null,
        string? fakeCodexScript = null,
        string? updateSource = null,
        string? installedVersion = null)
    {
        scionHome ??= new TempDirectory("scion-home");
        var port = FreeTcpPort();
        var startInfo = new ProcessStartInfo(LocateExecutable()) { UseShellExecute = false };
        startInfo.Environment["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"] = $"--remote-debugging-port={port}";
        startInfo.Environment["SCION_HOME"] = scionHome.Path;
        startInfo.Environment.Remove("SCION_TEST_FOLDER");
        startInfo.Environment.Remove("SCION_TEST_SHELL_LOG");
        startInfo.Environment.Remove("SCION_CLAUDE_PATH");
        startInfo.Environment.Remove("SCION_FAKE_CLAUDE_SCRIPT");
        startInfo.Environment.Remove("SCION_CODEX_PATH");
        startInfo.Environment.Remove("SCION_FAKE_CODEX_SCRIPT");
        startInfo.Environment.Remove("SCION_UPDATE_SOURCE");
        startInfo.Environment.Remove("SCION_TEST_INSTALLED_VERSION");

        startInfo.Environment["SCION_CLAUDE_PATH"] = claudePath ?? NoClaude;

        if (fakeClaudeScript is not null)
        {
            startInfo.Environment["SCION_FAKE_CLAUDE_SCRIPT"] = fakeClaudeScript;
        }

        startInfo.Environment["SCION_CODEX_PATH"] = codexPath ?? NoCodex;

        if (fakeCodexScript is not null)
        {
            startInfo.Environment["SCION_FAKE_CODEX_SCRIPT"] = fakeCodexScript;
        }

        startInfo.Environment["SCION_UPDATE_SOURCE"] = updateSource ?? NoUpdateSource;

        if (installedVersion is not null)
        {
            startInfo.Environment["SCION_TEST_INSTALLED_VERSION"] = installedVersion;
        }

        if (recordShell)
        {
            startInfo.Environment["SCION_TEST_SHELL_LOG"] = Path.Combine(scionHome.Path, "shell.log");
        }

        if (testFolder is not null)
        {
            startInfo.Environment["SCION_TEST_FOLDER"] = testFolder;
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The desktop app did not start.");
        var playwright = await Playwright.CreateAsync();

        try
        {
            var (browser, page) = await ConnectAsync(playwright, port);

            return new DesktopApp(process, playwright, browser, page, scionHome);
        }
        catch
        {
            Kill(process);
            playwright.Dispose();
            throw;
        }
    }

    /// <summary>The lines the recording shell wrote: <c>reveal &lt;path&gt;</c> or <c>open &lt;url&gt;</c>.</summary>
    public string[] ReadShellLog()
    {
        if (!File.Exists(ShellLog))
        {
            return [];
        }

        // The app may be appending right now, so the file is opened for sharing, not exclusively for reading.
        using var reader = new StreamReader(new FileStream(ShellLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task ScreenshotAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);

        // A window that is hidden or covered by another produces no frames, and the capture then waits for one until it
        // times out (seen on 2026-11-04: a different test in every full run, none when run alone). Bringing the page to the
        // front makes the window draw.
        // It does not always help: one capture in some sixty still stalls. It is tried once more, shorter; a second stall fails the
        // test, it is not ignored.
        var options = new PageScreenshotOptions { Path = Path.Combine(directory, name + ".png"), Timeout = 10_000 };
        for (var attempt = 1; ; attempt++)
        {
            await Page.BringToFrontAsync();
            try
            {
                await Page.ScreenshotAsync(options);

                return;
            }
            catch (TimeoutException) when (attempt == 1)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _browser.CloseAsync();
        }
        catch (PlaywrightException)
        {
            // The app may already be gone.
        }

        _playwright.Dispose();
        Kill(_process);
        ScionHome.Dispose();
    }

    private static async Task<(IBrowser Browser, IPage Page)> ConnectAsync(IPlaywright playwright, int port)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{port}");
                var page = browser.Contexts.SelectMany(context => context.Pages).FirstOrDefault();

                if (page is not null)
                {
                    return (browser, page);
                }
            }
            catch (PlaywrightException)
            {
                // WebView2 is not listening yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Could not connect to the desktop app over the Chrome DevTools Protocol.");
    }

    private static void Kill(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        process.WaitForExit();
        process.Dispose();
    }

    private static int FreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private static string LocateExecutable()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scion.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find scion.slnx above the test output folder.");
        }

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

        return Path.Combine(
            directory.FullName, "src", "Scion.Host.Desktop", "bin", configuration, "net10.0", "Scion.Host.Desktop.exe");
    }
}
