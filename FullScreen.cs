using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ShrineDock;

// A full-screen screen is left alone (2026-11-04, his ask: "anything opened on the other screen appears ONLY there,
// without taking focus from or covering the full-screen one").
//
// Before this, "a game is on this screen" meant "the game has the focus". The moment he clicked anything on the other
// screen, the game lost the focus, so the game screen's dock seam and top strip woke up again over the game. Now the game
// counts as long as it is the top window on its screen, whoever has the focus.
//
// Our pop-ups that need the keyboard (Start, Tasks, menus, settings) still take the focus while open (Start's search
// box needs his keys), but give it back to the window that had it when they close without him picking another window.
static class FullScreen
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);

    static readonly string[] Games = ["Wow", "WowB", "Cemu"];
    public static bool IsGame(string? exe) => exe != null && Games.Any(g => g.Equals(System.IO.Path.GetFileNameWithoutExtension(exe), StringComparison.OrdinalIgnoreCase));

    static IntPtr MonitorOf(Rect screen) =>
        MonitorFromPoint(new POINT { X = (int)(screen.Left + screen.Width / 2), Y = (int)(screen.Top + screen.Height / 2) }, 2);

    static bool Ours(IntPtr h) { GetWindowThreadProcessId(h, out var pid); return pid == (uint)Environment.ProcessId; }

    /// <summary>The window on top on that screen, below the always-on-top layer (the dock, strip, Aang, Rainmeter):
    /// what he sees there. Zero when only the desktop shows.</summary>
    public static IntPtr TopOn(Rect screen)
    {
        var mon = MonitorOf(screen); IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>   // top of the stack first
        {
            if (!IsWindowVisible(h) || IsIconic(h) || Ours(h)) return true;
            int ex = GetWindowLong(h, -20);
            if ((ex & 0x8) != 0) return true;                                                  // always-on-top layer
            if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return true;                          // tool windows
            if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            if (!GetWindowRect(h, out var r) || r.R - r.L < 50 || r.B - r.T < 50) return true;  // helper slivers
            var cls = new StringBuilder(64); GetClassName(h, cls, 64);
            if (cls.ToString() is "Progman" or "WorkerW") return false;                         // the desktop: nothing above it here
            if (MonitorFromWindow(h, 2) != mon) return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>WoW or Cemu is the top window on that screen (with or without the focus).</summary>
    public static bool GameOn(Rect screen)
    {
        var h = TopOn(screen); if (h == IntPtr.Zero) return false;
        GetWindowThreadProcessId(h, out var pid);
        return IsGame(StripWindow.ExePath(pid));
    }

    /// <summary>A game has the focus (its keys are his): Win and Ctrl+Shift+Esc are Windows' then.</summary>
    public static bool GameFocused()
    {
        var fg = GetForegroundWindow(); if (fg == IntPtr.Zero) return false;
        GetWindowThreadProcessId(fg, out var pid);
        return IsGame(StripWindow.ExePath(pid));
    }

    /// <summary>Gives one of our pop-ups the focus. Windows lets us when the click or key that opened it was ours; only if
    /// it refuses is the old Alt tap used, because that Alt lands in whatever has the focus (a game, a full-screen page).</summary>
    public static void Take(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (!SetForegroundWindow(h) && GetForegroundWindow() != h)
        {
            keybd_event(0x12, 0, 0, WinKey.Mine); SetForegroundWindow(h); keybd_event(0x12, 0, 2, WinKey.Mine);
            Program.Log("focus: needed the Alt tap");
        }
        w.Activate();
    }

    /// <summary>When <paramref name="w"/> closes, the window that had the focus before it gets it back, unless he went to
    /// another window meanwhile or something was opened from it (that new window should keep the focus).</summary>
    public static void GiveBackOnClose(Window w)
    {
        var before = GetForegroundWindow();
        if (before == IntPtr.Zero || Ours(before)) return;
        w.Closed += (_, _) => w.Dispatcher.BeginInvoke(() =>
        {
            if (!IsWindow(before) || IsIconic(before)) return;
            if ((DateTime.UtcNow - ScreenMove.LastLaunch).TotalSeconds < 5) return;   // he opened something from it
            var fg = GetForegroundWindow();
            if (fg == before) return;
            if (fg != IntPtr.Zero)
            {
                var cls = new StringBuilder(64); GetClassName(fg, cls, 64);
                bool desktop = cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
                if (!desktop && !Ours(fg)) return;                 // he clicked into another window: that one keeps it
                if (Ours(fg) && IsWindowVisible(fg)) return;      // another of our pop-ups is still open (Start under its menu)
            }
            if (SetForegroundWindow(before)) Program.Log("focus: given back after a pop-up closed");
        }, DispatcherPriority.Background);
    }
}
