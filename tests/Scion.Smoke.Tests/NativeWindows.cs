using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Scion.Smoke.Tests;

/// <summary>Lists and closes the visible top-level windows of one process (Windows only).</summary>
[SupportedOSPlatform("windows")]
internal static class NativeWindows
{
    private const uint CloseMessage = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    public static IReadOnlyList<string> TitlesOf(int processId) =>
        VisibleWindowsOf(processId).Select(window => window.Title).ToList();

    /// <summary>Closing a dialog is what a user does when cancelling it.</summary>
    public static void CloseAllExceptTitled(int processId, string keepTitle)
    {
        foreach (var window in VisibleWindowsOf(processId).Where(window => window.Title != keepTitle))
        {
            PostMessage(window.Handle, CloseMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>What the close button does: the window is asked to close and may refuse.</summary>
    public static void RequestClose(int processId, string title)
    {
        foreach (var window in VisibleWindowsOf(processId).Where(window => window.Title == title))
        {
            PostMessage(window.Handle, CloseMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static List<(IntPtr Handle, string Title)> VisibleWindowsOf(int processId)
    {
        var windows = new List<(IntPtr, string)>();

        EnumWindows(
            (window, _) =>
            {
                GetWindowThreadProcessId(window, out var windowProcessId);

                if (windowProcessId == processId && IsWindowVisible(window))
                {
                    var title = new StringBuilder(256);
                    GetWindowText(window, title, title.Capacity);
                    windows.Add((window, title.ToString()));
                }

                return true;
            },
            IntPtr.Zero);

        return windows;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
