using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace ShrineDock;

// Remembers where he left the clock, the PC gauges and the settings seal, across refreshes, restarts and
// screen 2 coming and going. Rainmeter's own SavePosition can't do it (screen 2 hot-plugs scramble it), so:
//  - every 2 s it looks where each widget is; a move is his only if the mouse button went down ON that widget
//    (a real drag), and is saved once it settles, relative to the screen it's on, keyed by that monitor's ID
//    (BLD06AB is the Acer). A click anywhere else no longer counts: on 2026-11-01 a click elsewhere while Lively
//    swapped wallpapers saved scrambled spots and sent his clock off the bottom of the screen.
//  - a move nobody made (a refresh, Explorer restarting, a screen leaving) is put back from the saved spot
//  - a widget whose screen is gone waits on screen 1 and goes home when that screen returns
class WidgetKeeper
{
    record Spot(string Monitor, int X, int Y);
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShrineDock", "widgets.json");
    readonly Dictionary<string, Spot> spots;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly Func<IntPtr> clockHwnd;
    readonly Action<int, int> moveClock;
    DateTime quietUntil = DateTime.MinValue, expectUntil = DateTime.MinValue;
    readonly Dictionary<string, DateTime> grabbed = [];   // when the mouse last went down on each widget
    bool wasDown;
    readonly Dictionary<string, (int x, int y)> seen = [];

    public static WidgetKeeper? Current { get; private set; }

    public WidgetKeeper(Func<IntPtr> clockHwnd, Action<int, int> moveClock)
    {
        Current = this;
        this.clockHwnd = clockHwnd; this.moveClock = moveClock;
        try { spots = JsonSerializer.Deserialize<Dictionary<string, Spot>>(File.ReadAllText(FilePath)) ?? []; } catch { spots = []; }
        timer.Tick += (_, _) => Check();
        timer.Start();
        var mouse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        // the low bit catches a click that came and went between two looks
        mouse.Tick += (_, _) =>
        {
            bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            if (down && !wasDown && GetCursorPos(out var pt))   // a fresh press: which widget is under the cursor?
                foreach (var (name, h) in Widgets())
                    if (GetWindowRect(h, out var r) && pt.X >= r.L && pt.X < r.R && pt.Y >= r.T && pt.Y < r.B) grabbed[name] = DateTime.Now;
            if (down) foreach (var k in grabbed.Keys.ToList()) if ((DateTime.Now - grabbed[k]).TotalSeconds < 60) grabbed[k] = DateTime.Now;   // still held
            wasDown = down;
        };
        mouse.Start();
    }

    /// <summary>Screens are changing: don't save anything for a few seconds, then put everything back.</summary>
    public void ScreensChanged() { quietUntil = DateTime.Now.AddSeconds(4); seen.Clear(); }

    /// <summary>He's moving them himself through the panel (size, face, the clock pulling the gauges along): save what follows.</summary>
    public void Expect() => expectUntil = DateTime.Now.AddSeconds(3);

    /// <summary>The widgets are about to get new homes (Reset, a new clock face): forget the old ones, settle, then learn the new.</summary>
    public void Forget()
    {
        spots.Clear(); seen.Clear(); quietUntil = DateTime.Now.AddSeconds(3);
        try { File.Delete(FilePath); } catch { }
    }

    // ---- the widgets and where they are now
    IEnumerable<(string name, IntPtr hwnd)> Widgets()
    {
        var c = clockHwnd(); if (c != IntPtr.Zero && IsWindowVisible(c)) yield return ("Clock", c);
        var rm = Process.GetProcessesByName("Rainmeter").FirstOrDefault(); if (rm == null) yield break;
        var found = new List<(string, IntPtr)>();
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var pid); if (pid != rm.Id || !IsWindowVisible(h)) return true;
            var t = new StringBuilder(260); GetWindowText(h, t, 260); var title = t.ToString();
            if (title.EndsWith(@"Shrine\PC\PC.ini", StringComparison.OrdinalIgnoreCase)) found.Add(("PC", h));
            else if (title.EndsWith(@"Shrine\Seal\Seal.ini", StringComparison.OrdinalIgnoreCase)) found.Add(("Seal", h));
            else if (title.EndsWith(@"Shrine\Clock\Clock.ini", StringComparison.OrdinalIgnoreCase) && !ClockWindow.Face().analog) found.Add(("Clock", h));
            return true;
        }, IntPtr.Zero);
        foreach (var f in found) yield return f;
    }

    void Check()
    {
        var screens = Screens.All();
        bool settling = DateTime.Now < quietUntil;
        bool panelMoved = DateTime.Now < expectUntil;
        foreach (var (name, h) in Widgets().ToList())
        {
            GetWindowRect(h, out var r);
            var now = (r.L, r.T);
            bool moved = seen.TryGetValue(name, out var was) && was != now;
            seen[name] = now;
            if (settling) continue;
            if (!spots.TryGetValue(name, out var spot)) { Save(name, now, screens); continue; } // first sight: remember it
            var home = Home(spot, screens, r.R - r.L, r.B - r.T);
            if (home == null) continue; // its screen is away: leave it where Windows put it, go home on its return
            if (home.Value == now) continue;
            bool dragged = grabbed.TryGetValue(name, out var g) && (DateTime.Now - g).TotalSeconds < 3;
            if (moved && (dragged || panelMoved)) { Save(name, now, screens); continue; } // he dragged it, or moved it from the panel
            Program.Log($"widgets: {name} was moved to {now.Item1},{now.Item2} without a drag; back to {home.Value.x},{home.Value.y}");
            Place(name, h, home.Value.x, home.Value.y);
            seen[name] = home.Value;
        }
    }

    // a home is always fully on its screen: on 2026-11-03 a saved Y of 1052 put the clock under the Acer's bottom edge
    static (int x, int y)? Home(Spot s, List<Screens.Screen> screens, int w, int h)
    {
        var scr = screens.FirstOrDefault(x => MonitorId(x.Device) == s.Monitor);
        if (scr == null) return null;
        var a = scr.Area;
        int x = (int)Math.Clamp(a.Left + s.X, a.Left, Math.Max(a.Left, a.Right - w));
        int y = (int)Math.Clamp(a.Top + s.Y, a.Top, Math.Max(a.Top, a.Bottom - h));
        return (x, y);
    }

    void Save(string name, (int x, int y) at, List<Screens.Screen> screens)
    {
        var scr = screens.FirstOrDefault(s => s.Area.Contains(new Point(at.x + 20, at.y + 20))) ?? screens.FirstOrDefault(s => s.Primary);
        if (scr == null) return;
        spots[name] = new(MonitorId(scr.Device), at.x - (int)scr.Area.Left, at.y - (int)scr.Area.Top);
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(spots, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }

    void Place(string name, IntPtr h, int x, int y)
    {
        if (name == "Clock" && h == clockHwnd()) { moveClock(x, y); return; }
        var config = name == "PC" ? @"Shrine\PC" : name == "Seal" ? @"Shrine\Seal" : @"Shrine\Clock";
        try { Process.Start(new ProcessStartInfo(@"C:\Program Files\Rainmeter\Rainmeter.exe", $"!Move {x} {y} \"{config}\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { }
    }

    /// <summary>"BLD06AB" from MONITOR\BLD06AB\{...}: the monitor's own ID, which stays put when Shadow renumbers screens.</summary>
    static string MonitorId(string device)
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (!EnumDisplayDevices(device, 0, ref dd, 0) || string.IsNullOrEmpty(dd.DeviceID)) return device;
        var parts = dd.DeviceID.Split('\\');
        return parts.Length > 1 ? parts[1] : dd.DeviceID;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string? device, uint index, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
}
