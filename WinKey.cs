using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShrineDock;

// The Win key opens our Start on the screen the mouse is on (his rule, 2026-11-02: on the Acer it must open on the
// Acer). A low-level keyboard hook on its own thread (so a busy dock can never make Windows drop it) holds each Win
// press back: a lone tap opens our Start; any key pressed with it (Win+E, Win+Shift+S, Win+L...) sends the held Win
// on first, so every Windows shortcut still works. Games keep Windows' own Win key, and so does "Windows Start" in
// the settings. Ctrl+Esc always opens Windows' Start. Ctrl+Shift+Esc opens Shrine Tasks (sheet 21) instead of Windows'
// Task Manager; if the dock is stuck, Windows skips a hook that doesn't answer and its own Task Manager opens.
static class WinKey
{
    /// <summary>Marks keys the dock sends itself, so the hook lets them through.</summary>
    public static readonly UIntPtr Mine = (UIntPtr)0x5348524E; // "SHRN"
    /// <summary>Windows keeps its own Win key while this is true (a game is running, or he chose Windows' Start).
    /// Read on every key, so it's a plain flag the dock keeps current, never a lookup.</summary>
    public static volatile bool LetWindowsHaveIt;
    static Action? tapped;
    /// <summary>Ctrl+Shift+Esc: Shrine Tasks. Null leaves the keys to Windows.</summary>
    public static Action? TaskKeys;
    static bool escHeld;
    static IntPtr hook;
    static HookProc? proc; // held in a field: a collected delegate would crash the hook
    static bool held, combo;
    static uint heldVk;
    // His report (2026-11-02): Start opened after every snip. The Shadow app plays his Mac's Cmd+Shift+4 as a Windows
    // shortcut, then sends the still-held Cmd as a fresh Win press, and letting go of it looked like a lone tap. A tap
    // right after a shortcut, or a Win held long, is never Start.
    static long downAt, comboAt = -10_000;

    public static void Start(Action onTap)
    {
        tapped = onTap;
        var t = new Thread(() =>
        {
            proc = Hook;
            using var me = Process.GetCurrentProcess();
            hook = SetWindowsHookEx(13 /*WH_KEYBOARD_LL*/, proc, GetModuleHandle(me.MainModule!.ModuleName), 0);
            Program.Log(hook == IntPtr.Zero ? $"start: the Win key hook failed ({Marshal.GetLastWin32Error()})" : "start: the Win key opens our Start");
            while (GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessage(ref m); }
        }) { IsBackground = true, Name = "WinKey" };
        t.Start();
    }

    static IntPtr Hook(int code, IntPtr msg, IntPtr data)
    {
        if (code < 0) return CallNextHookEx(hook, code, msg, data);
        var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(data);
        if (k.extra == Mine) return CallNextHookEx(hook, code, msg, data);
        bool down = msg == 0x100 || msg == 0x104, up = msg == 0x101 || msg == 0x105;
        bool win = k.vk == 0x5B || k.vk == 0x5C;

        if (k.vk == 0x1B && TaskKeys is { } tk)
        {
            if (down && !LetWindowsHaveIt && (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x10) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) == 0 && !held)
            {
                if (!escHeld) ThreadPool.QueueUserWorkItem(_ => { try { tk(); } catch (Exception ex) { Program.Log("tasks: " + ex.Message); } });
                escHeld = true; return 1;
            }
            if (up && escHeld) { escHeld = false; return 1; }
        }

        if (win && down)
        {
            if (held) return 1;                                     // auto-repeat while held
            if (LetWindowsHaveIt) return CallNextHookEx(hook, code, msg, data);
            held = true; combo = false; heldVk = k.vk; downAt = Environment.TickCount64; return 1;    // hold it back for now
        }
        if (win && up && held && k.vk == heldVk)
        {
            held = false;
            var now = Environment.TickCount64;
            if (combo) { comboAt = now; return CallNextHookEx(hook, code, msg, data); } // Windows saw the Win we sent on; let it see the release
            if (now - comboAt < 2000 || now - downAt > 800) return 1;   // the tail of a shortcut, or a held Win: nothing opens
            ThreadPool.QueueUserWorkItem(_ => { try { tapped!(); } catch (Exception ex) { Program.Log("start: " + ex.Message); } });
            return 1;                                               // a lone tap: ours, Windows never sees it
        }
        if (held && down && !combo)
        {
            // a shortcut: Windows gets the Win press first, then this key, as if we were never here
            combo = true; keybd_event((byte)heldVk, 0, 1 /*EXTENDED*/, Mine);
        }
        return CallNextHookEx(hook, code, msg, data);
    }

    [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT { public uint vk, scan, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr w, l; public uint time; public int x, y; }
    delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
