using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ShrineDock;

// An app opened from a dock opens on that dock's screen (2026-11-04, his ask: "can dock also open windows in the screen
// they were clicked on?"). Windows puts a new window wherever the app last was, so after a launch we watch for the app's
// first new window (up to 10 s) and, if it came up on another screen, move it over: same size, same place on the screen,
// maximized stays maximized. Windows that were already open are left where they are.
static class ScreenMove
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    /// <summary>Call just before starting the app: watches for its new window and brings it to <paramref name="to"/>.</summary>
    public static void AfterLaunch(string? process, Rect to)
    {
        if (Screens.All().Count < 2) return;
        var before = new HashSet<IntPtr>(StripWindow.OpenWindows());
        var started = DateTime.UtcNow;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        t.Tick += (_, _) =>
        {
            try
            {
                if ((DateTime.UtcNow - started).TotalSeconds > 10) { t.Stop(); return; }
                foreach (var h in StripWindow.OpenWindows())
                {
                    if (before.Contains(h) || (process != null && !IsProcess(h, process))) continue;
                    t.Stop();
                    // give the app a moment to put its window where it wants, then move it once
                    var once = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                    once.Tick += (_, _) => { once.Stop(); try { MoveTo(h, to); } catch { } };
                    once.Start();
                    return;
                }
            }
            catch { t.Stop(); }
        };
        t.Start();
    }

    static bool IsProcess(IntPtr h, string process)
    {
        GetWindowThreadProcessId(h, out var pid);
        try { using var p = Process.GetProcessById((int)pid); return p.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    /// <summary>Onto the screen <paramref name="to"/>, keeping size and place on the screen; nothing when it is there already.</summary>
    static void MoveTo(IntPtr h, Rect to)
    {
        if (!GetWindowRect(h, out var r)) return;
        var c = new Point((r.L + r.R) / 2.0, (r.T + r.B) / 2.0);
        if (to.Contains(c)) return;
        var from = Screens.All().FirstOrDefault(s => s.Area.Contains(c))?.Area ?? to;
        bool max = IsZoomed(h);
        if (max || IsIconic(h)) ShowWindow(h, 9);
        if (!GetWindowRect(h, out r)) return;
        int w = r.R - r.L, hgt = r.B - r.T;
        int x = (int)Math.Clamp(to.Left + (r.L - from.Left) * to.Width / Math.Max(1, from.Width), to.Left, Math.Max(to.Left, to.Right - w));
        int y = (int)Math.Clamp(to.Top + (r.T - from.Top) * to.Height / Math.Max(1, from.Height), to.Top, Math.Max(to.Top, to.Bottom - hgt));
        MoveWindow(h, x, y, w, hgt, true);
        if (max) ShowWindow(h, 3);
    }
}
