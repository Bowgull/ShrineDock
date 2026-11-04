using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ShrineDock;

// Apps that want him (sheet 22 A, his pick 2026-11-04). Windows asks for attention by flashing the app's taskbar button;
// his taskbar is hidden, so ShrineDock listens on the shell hook instead (HSHELL_FLASH) and keeps the window here until
// he goes to that app. The dock's line glows amber while anything is waiting; the app's tile bounces when the dock rises.
static class Attention
{
    [DllImport("user32.dll")] static extern bool RegisterShellHookWindow(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);

    const int Activated = 4, RudeActivated = 0x8004, Destroyed = 2, Flash = 0x8006;
    static uint shellMsg;
    static bool hooked;
    /// <summary>The windows waiting for him, with their program's file name (no extension).</summary>
    static readonly Dictionary<IntPtr, string> waiting = [];
    public static event Action? Changed;

    public static bool Any { get { Prune(); return waiting.Count > 0; } }
    public static bool Wants(IntPtr h) => waiting.ContainsKey(h);
    /// <summary>True when a window of this program (and, for apps that share a program, with a title ending so) is waiting.</summary>
    public static bool Wants(string process, string? titleEnds = null)
    {
        Prune();
        foreach (var (h, exe) in waiting)
        {
            if (!exe.Equals(process, StringComparison.OrdinalIgnoreCase)) continue;
            if (titleEnds == null || StripWindow.TitleOf(h).EndsWith(titleEnds, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Listens through one of the dock's own windows (the first one asks; one hook is enough).</summary>
    public static void Listen(IntPtr hwnd)
    {
        if (hooked || hwnd == IntPtr.Zero) return;
        shellMsg = RegisterWindowMessage("SHELLHOOK");
        if (!RegisterShellHookWindow(hwnd)) { Program.Log("attention: shell hook refused"); return; }
        hooked = true;
        HwndSource.FromHwnd(hwnd)?.AddHook(Hook);
        Program.Log("attention: listening");
    }

    static IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (shellMsg == 0 || msg != (int)shellMsg) return IntPtr.Zero;
        int code = w.ToInt32();
        if (code == Flash)
        {
            // a window already in front is not asking for anything
            if (l == GetForegroundWindow() || waiting.ContainsKey(l)) return IntPtr.Zero;
            GetWindowThreadProcessId(l, out var pid);
            var exe = StripWindow.ExePath(pid);
            if (exe == null) return IntPtr.Zero;
            waiting[l] = System.IO.Path.GetFileNameWithoutExtension(exe);
            Program.Log($"attention: {waiting[l]} wants him");
            Changed?.Invoke();
        }
        else if (code is Activated or RudeActivated) Seen(l);
        else if (code == Destroyed && waiting.Remove(l)) Changed?.Invoke();
        return IntPtr.Zero;
    }

    /// <summary>He went to it: that window, and the rest of the same app, stop waiting.</summary>
    public static void Seen(IntPtr h)
    {
        if (h == IntPtr.Zero) return;
        GetWindowThreadProcessId(h, out var pid);
        var exe = StripWindow.ExePath(pid);
        var name = exe == null ? null : System.IO.Path.GetFileNameWithoutExtension(exe);
        // a shared program (Samara among Aang's electron windows): only that window
        bool shared = name != null && name.Equals("electron", StringComparison.OrdinalIgnoreCase);
        var gone = waiting.Where(p => p.Key == h || (!shared && name != null && p.Value.Equals(name, StringComparison.OrdinalIgnoreCase))).Select(p => p.Key).ToList();
        if (gone.Count == 0) return;
        foreach (var g in gone) waiting.Remove(g);
        Changed?.Invoke();
    }

    static void Prune()
    {
        var dead = waiting.Keys.Where(k => !IsWindow(k)).ToList();
        foreach (var d in dead) waiting.Remove(d);
    }
}
