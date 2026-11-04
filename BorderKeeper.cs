using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShrineDock;

// Window edges in the Shrine look, for every app: the active window gets a soft, dim cyan border, the others a
// near-invisible slate one, all with rounded corners and slate title bars where Windows draws them.
// Samara and Aang draw their own edges, so Windows' border is turned off for them; WoW and Cemu are never touched.
// Windows lets another program set these (DWM window attributes), so this needs no install and no mod.
static class BorderKeeper
{
    const int BorderColor = 34, CaptionColor = 35, TextColor = 36, Corners = 33;
    const int Active = 0x00806E1E;       // COLORREF (BGR): rgb(30, 110, 128), the Shrine cyan, dimmed
    const int Inactive = 0x00261C12;     // rgb(18, 28, 38), slate, barely there
    const int Caption = 0x003E2A10;      // rgb(16, 42, 62), the panels' slate
    const int Text = 0x00FFF2C4;         // rgb(196, 242, 255), ice
    const int None = unchecked((int)0xFFFFFFFE);
    static readonly HashSet<string> Untouched = new(StringComparer.OrdinalIgnoreCase) { "WowB", "Wow", "Cemu" };
    static readonly HashSet<string> OwnEdges = new(StringComparer.OrdinalIgnoreCase) { "electron", "Aang" };
    // Windows' own pop-up surfaces (Start, which lives in SearchHost on his build, search, quick settings, the touch
    // keyboard): their host windows are larger than what they draw, so any border on them, even Windows' default
    // grey, floats as an empty frame around the Start menu. Their border is turned off.
    static readonly HashSet<string> Shell = new(StringComparer.OrdinalIgnoreCase)
        { "StartMenuExperienceHost", "SearchHost", "SearchApp", "ShellExperienceHost", "ShellHost", "TextInputHost", "LockApp" };
    const int Default = unchecked((int)0xFFFFFFFF);
    static IntPtr lastActive;
    static WinEventProc? keep; // held so the callback isn't collected
    static readonly List<IntPtr> hooks = [];

    public static void Start()
    {
        keep = (_, ev, h, idObject, _, _, _) =>
        {
            if (idObject != 0) return; // the window itself, not a part of it
            if (ev == 0x0003) Foreground(h); else Paint(h, h == GetForegroundWindow());
        };
        hooks.Add(SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, keep, 0, 0, 0));          // EVENT_SYSTEM_FOREGROUND
        hooks.Add(SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, keep, 0, 0, 0));          // EVENT_OBJECT_SHOW
        var fg = GetForegroundWindow();
        // earlier builds framed Windows' hidden helpers too (Start's host, snap previews), and those windows live on:
        // give every window that isn't a real app window Windows' own defaults back
        EnumWindows((h, _) =>
        {
            if (StripWindow.IsAppWindow(h)) return true;
            GetWindowThreadProcessId(h, out var pid); if (pid == Environment.ProcessId) return true;
            string name; try { name = Process.GetProcessById((int)pid).ProcessName; } catch { return true; }
            if (OwnEdges.Contains(name) || Untouched.Contains(name)) return true; // Samara and Aang keep their borders off
            Set(h, BorderColor, Shell.Contains(name) ? None : Default); Set(h, CaptionColor, Default); Set(h, TextColor, Default); Set(h, Corners, 0);
            return true;
        }, IntPtr.Zero);
        foreach (var h in StripWindow.OpenWindows()) Paint(h, h == fg);
        lastActive = fg;
    }

    static void Foreground(IntPtr h)
    {
        if (lastActive != IntPtr.Zero && lastActive != h) Paint(lastActive, false);
        Paint(h, true); lastActive = h;
    }

    static void Paint(IntPtr h, bool active)
    {
        if (h == IntPtr.Zero || GetWindow(h, 4) != IntPtr.Zero || !IsWindowVisible(h)) return; // top-level, unowned
        GetWindowThreadProcessId(h, out var pid);
        string name; try { name = Process.GetProcessById((int)pid).ProcessName; } catch { return; }
        if (Untouched.Contains(name) || pid == Environment.ProcessId) return;
        if (Shell.Contains(name)) { Set(h, BorderColor, None); return; } // even Windows' default grey shows there
        if (!StripWindow.IsAppWindow(h)) return; // snap previews and other helpers that pop up while dragging
        if (OwnEdges.Contains(name)) { Set(h, BorderColor, None); return; }
        Set(h, BorderColor, active ? Active : Inactive);
        Set(h, Corners, 2); // DWMWCP_ROUND
        Set(h, CaptionColor, Caption);
        Set(h, TextColor, Text);
    }
    static void Set(IntPtr h, int attr, int value) => DwmSetWindowAttribute(h, attr, ref value, 4);

    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
