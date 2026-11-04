using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ShrineDock;

// The arrival, conducted from one place (2026-11-02, after a start where everything fired on its own: the chime played
// before the garden existed, the clock and gauges popped in first, the Acer came up without its garden).
// One trigger path: this app's own start (sign-in), and Windows' session events (unlock, reconnect). Lock and
// disconnect hide the desktop pieces so the next arrival can reveal them. The order, every time:
//   1. wait for the screens to settle and for Lively to have a garden on EVERY screen (fix any screen without one)
//   2. the garden's "Welcome back, Josh" on the main screen, the chime with its line
//   3. then the clock, the gauges and seal, and the docks and top strips fade in, one after another
// A guard keeps it to one run at a time and one show a minute; in a game it reveals at once, with no show and no sound.
static class Arrival
{
    const string Res = @"C:\Users\Shadow\Documents\Rainmeter\Skins\Shrine\@Resources\";
    const string Rm = @"C:\Program Files\Rainmeter\Rainmeter.exe";
    static readonly string LocalApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string LivelyExe = Path.Combine(LocalApp, @"Programs\Lively Wallpaper\Lively.exe");
    static readonly string Garden = Path.Combine(LocalApp, @"Lively Wallpaper\Library\wallpapers\shrinegarden");

    static Func<IEnumerable<Window>> pieces = () => [];   // the dock, strips and clock windows, whichever exist now
    static ClockWindow? clock;
    static DockWindow? dock;
    static bool running, hidden;
    public static bool IsHidden => hidden;
    static DateTime lastShow = DateTime.MinValue, lastDisplayChange = DateTime.MinValue;
    static Dispatcher? ui;

    public static void Init(DockWindow d, ClockWindow c, Func<IEnumerable<Window>> windows)
    {
        dock = d; clock = c; pieces = windows; ui = Dispatcher.CurrentDispatcher;
        SystemEvents.SessionSwitch += (_, e) => ui.BeginInvoke(() =>
        {
            Program.Log($"arrival: session {e.Reason}");
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock: case SessionSwitchReason.ConsoleDisconnect: case SessionSwitchReason.RemoteDisconnect:
                    Hide(); break;
                case SessionSwitchReason.SessionUnlock: case SessionSwitchReason.ConsoleConnect: case SessionSwitchReason.RemoteConnect: case SessionSwitchReason.SessionLogon:
                    _ = Run($"session {e.Reason}"); break;
            }
        });
    }
    public static void DisplayChanged() => lastDisplayChange = DateTime.Now;

    static bool InGame() => Process.GetProcessesByName("WowB").Length > 0 || Process.GetProcessesByName("Cemu").Length > 0;

    /// <summary>Everything that should wait for its turn steps out of sight (used on lock/disconnect and before a start).</summary>
    public static void Hide()
    {
        hidden = true;
        foreach (var w in pieces()) { w.BeginAnimation(UIElement.OpacityProperty, null); w.Opacity = 0; }
        if (Process.GetProcessesByName("Rainmeter").Length > 0) foreach (var cfg in Widgets()) Bang("!HideFade", cfg);
    }
    static IEnumerable<string> Widgets() { yield return @"Shrine\PC"; yield return @"Shrine\Seal"; if (!ClockWindow.Face().analog) yield return @"Shrine\Clock"; }

    public static async Task Run(string why, bool force = false, bool quiet = false)
    {
        if (running) { Program.Log($"arrival: {why} ignored, one is already running"); return; }
        running = true;
        try
        {
            Program.Log($"arrival: {why}");
            if (!hidden) Hide();
            bool game = InGame(), show = !game && !quiet && (force || (DateTime.Now - lastShow).TotalSeconds > 60);
            // a fresh garden page waits under black for the welcome instead of showing itself first (it reads arrival.js)
            if (show) Hold(true);
            // 1. the screens settle (no change for 3 s, at most 20 s), then a garden on every screen
            var until = DateTime.Now.AddSeconds(20);
            while (DateTime.Now < until && (DateTime.Now - lastDisplayChange).TotalSeconds < 3) await Task.Delay(250);
            await EnsureGarden();
            // 2. the show: "Welcome back, Josh" on every screen, one chime with its glowing line
            if (show)
            {
                lastShow = DateTime.Now;
                foreach (var s in Screens.All()) Start(LivelyExe, $"setprop --monitor {DisplayNumber(s.Device)} --property \"arrive=true\"");
                Hold(false);
                await Task.Delay(2300);
                Chime();
                await Task.Delay(1300);   // the garden opens at 3.2 s
            }
            else Program.Log(game ? "arrival: a game is running, no show" : quiet ? "arrival: a quiet restart, no show" : "arrival: shown less than a minute ago, no show");
            // 3. the desktop pieces, in turn: clock, then gauges and seal, then docks and strips
            Reveal();
        }
        catch (Exception e) { Program.Log("arrival: " + e.Message); Reveal(); }
        finally { running = false; Hold(false); }
    }

    static async void Reveal()
    {
        hidden = false;
        if (clock != null) Fade(clock, 0);
        if (Process.GetProcessesByName("Rainmeter").Length == 0) { Start(Rm, ""); Program.Log("arrival: Rainmeter started"); } // its skins fade in on load
        else { await Task.Delay(250); foreach (var cfg in Widgets()) Bang("!ShowFade", cfg); }
        await Task.Delay(450);
        int i = 0; foreach (var w in pieces().Where(w => w != clock)) Fade(w, 120 * i++);
        await Task.Delay(500); dock?.SayHello();
    }
    static void Fade(Window w, int delayMs)
    {
        var a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        a.Completed += (_, _) => w.Opacity = 1;
        w.BeginAnimation(UIElement.OpacityProperty, a);
    }

    // ---- a garden on every screen: Lively keeps its own list of screens, and when a screen arrives late (the Acer
    // after sign-in) that list can go stale, so asking it for that screen does nothing. Ask once; if a screen still has
    // no garden, restart Lively cleanly (tools\clean-lively-layout.ps1), which re-reads the screens and lays them all.
    public static async Task EnsureGarden()
    {
        // Lively is started from here, not from Windows' Run list, so the garden can't show itself before the welcome
        if (Process.GetProcessesByName("Lively").Length == 0) { Program.Log("garden: starting Lively"); Start(LivelyExe, ""); }
        for (int t = 0; t < 40 && Process.GetProcessesByName("Lively").Length == 0; t++) await Task.Delay(500);
        // a starting Lively lays its screens itself; give it time before calling a screen missing, then let the pages load.
        // 8 s, not 30: a good start lays them in 6-10 s, and when its saved layout is stale (Shadow renumbered the
        // screens) it lays nothing at all, so waiting longer only kept him staring at a bare desktop (2026-11-03: 80 s)
        int waited = 0;
        for (; waited < 16 && MissingScreens().Count > 0; waited++) await Task.Delay(500);
        if (waited > 0 && MissingScreens().Count == 0) await Task.Delay(2000);
        var missing = MissingScreens();
        if (missing.Count == 0) { Program.Log("garden: on every screen"); return; }
        Program.Log($"garden: missing on {string.Join(", ", missing.Select(m => m.device))}, asking Lively");
        foreach (var (device, _) in missing) Start(LivelyExe, $"setwp --file \"{Garden}\" --monitor {DisplayNumber(device)}");
        for (int t = 0; t < 16 && MissingScreens().Count > 0; t++) await Task.Delay(500);
        if (MissingScreens().Count == 0) { Program.Log("garden: back on every screen"); await Task.Delay(2500); return; }
        Program.Log("garden: Lively's screen list is stale, restarting it cleanly");
        var p = Start("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"C:\\Users\\Shadow\\Documents\\Shrine Desktop\\tools\\clean-lively-layout.ps1\"");
        if (p != null) await Task.Run(() => p.WaitForExit(60000));
        for (int t = 0; t < 30 && MissingScreens().Count > 0; t++) await Task.Delay(500);
        Program.Log(MissingScreens().Count == 0 ? "garden: back on every screen after the restart" : "garden: STILL missing after the restart");
        await Task.Delay(2500); // the pages load
    }
    static List<(string device, string id)> MissingScreens()
    {
        var players = new List<string>();
        try
        {
            using var q = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='Lively.Player.WebView2.exe'");
            foreach (var o in q.Get()) players.Add(o["CommandLine"]?.ToString() ?? "");
        }
        catch { }
        return Screens.All().Select(s => (s.Device, id: Screens.MonitorId(s.Device))).Where(s => !players.Any(c => c.Contains("#" + s.id + "#"))).ToList();
    }
    static int DisplayNumber(string device) => int.TryParse(new string(device.Where(char.IsDigit).ToArray()), out var n) ? n : 1;
    // arrival.js sits beside the garden's index.html: while it holds a recent time, a page that loads stays black until
    // "arrive" comes (or 25 s pass). Cleared as soon as the welcome is sent, so a later Lively restart shows at once.
    static void Hold(bool on)
    {
        try { File.WriteAllText(Path.Combine(Garden, "arrival.js"), $"window.SHRINE_ARRIVAL={(on ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0)};"); }
        catch (Exception e) { Program.Log("arrival: hold " + e.Message); }
    }

    static void Chime()
    {
        if (File.Exists(Res + @"Sounds\chime-off")) return;
        var wav = Res + @"Sounds\" + (File.Exists(Res + @"Sounds\chime-quiet") ? "signin-quiet.wav" : "signin.wav");
        Task.Run(() => { try { new System.Media.SoundPlayer(wav).PlaySync(); } catch (Exception e) { Program.Log("arrival: chime " + e.Message); } });
    }
    static void Bang(string bang, string cfg) => Start(Rm, $"{bang} \"{cfg}\"");
    static Process? Start(string exe, string args)
    {
        try { return Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true }); }
        catch (Exception e) { Program.Log($"arrival: {Path.GetFileName(exe)}: {e.Message}"); return null; }
    }
}
