using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShrineDock;

// The Shrine dock (sheet 08): sleeps as a thin glowing seam at the bottom of screen 1, rises when the mouse reaches
// the bottom edge, lifts only the icon under the mouse, names it after a short pause, and sinks again after a delay.
// It watches the cursor itself (cheap polling), so it always knows where the mouse is, and steps aside for games.
static class Program
{
    static FileSystemWatcher? Face; // kept in a field: a watcher nothing holds gets collected and goes quiet
    const string HelloEvent = @"Local\ShrineDock.Hello", SettingsEvent = @"Local\ShrineDock.Settings", SnapshotEvent = @"Local\ShrineDock.Snapshot", ArriveEvent = @"Local\ShrineDock.Arrive",
        SoundEvent = @"Local\ShrineDock.Sound", VolumeEvent = @"Local\ShrineDock.Volume", TasksEvent = @"Local\ShrineDock.Tasks";

    public static void Log(string m) { try { File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "dock.log"), $"{DateTime.Now:T} {m}\n"); } catch { } }

    [STAThread]
    static void Main(string[] args)
    {
        // the native Reddit feed CPU gate: its own process, before the single-dock lock, so the dock is untouched
        if (args.Contains("--feed-spike")) { FeedSpike.Run(args); return; }
        if (args.Contains("--settings"))
        {
            // the seal on the desktop opens the settings panel in the running dock
            if (EventWaitHandle.TryOpenExisting(SettingsEvent, out var se)) se.Set();
            return;
        }
        if (args.Contains("--sound") || args.Contains("--volume"))
        {
            // the Sound page, or the strip's volume drop-down (for checking them without clicks)
            if (EventWaitHandle.TryOpenExisting(args.Contains("--sound") ? SoundEvent : VolumeEvent, out var so)) so.Set();
            return;
        }
        if (args.Contains("--snapshot"))
        {
            // a picture of the live clock, for checking it (dock folder\clock-snapshot.png)
            if (EventWaitHandle.TryOpenExisting(SnapshotEvent, out var sn)) sn.Set();
            return;
        }
        if (args.Contains("--tasks"))
        {
            // Shrine Tasks in the running dock (what Ctrl+Shift+Esc and the Start seal open)
            if (EventWaitHandle.TryOpenExisting(TasksEvent, out var te)) te.Set(); else Log("tasks: --tasks found no running dock");
            return;
        }
        if (args.Contains("--arrive"))
        {
            // the same arrival an unlock or reconnect runs (for testing it without locking the PC)
            if (EventWaitHandle.TryOpenExisting(ArriveEvent, out var ae)) ae.Set();
            return;
        }
        if (args.Contains("--hello"))
        {
            // The arrival asks a running dock to rise once and settle.
            if (EventWaitHandle.TryOpenExisting(HelloEvent, out var ev)) ev.Set();
            return;
        }
        using var single = new Mutex(true, @"Local\ShrineDock.Single", out bool first);
        if (!first) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        if (!Settings.Load().FreeScreen2) Screens.KeepAcerLeft();
        var primary = Screens.All().FirstOrDefault(s => s.Primary)?.Area ?? new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        var dock = new DockWindow(primary, new EventWaitHandle(false, EventResetMode.AutoReset, HelloEvent));
        var settingsAsk = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEvent);
        new Thread(() => { while (true) { settingsAsk.WaitOne(); app.Dispatcher.BeginInvoke(SettingsPanel.Toggle); } }) { IsBackground = true }.Start();
        var soundAsk = new EventWaitHandle(false, EventResetMode.AutoReset, SoundEvent);
        new Thread(() => { while (true) { soundAsk.WaitOne(); app.Dispatcher.BeginInvoke(() => SettingsPanel.OpenOn("Sound")); } }) { IsBackground = true }.Start();
        var volumeAsk = new EventWaitHandle(false, EventResetMode.AutoReset, VolumeEvent);
        new Thread(() => { while (true) { volumeAsk.WaitOne(); app.Dispatcher.BeginInvoke(() => VolumePopup.Toggle(new Point(primary.Right - 220, primary.Top + 72))); } }) { IsBackground = true }.Start();
        var strip = new StripWindow(() => dock.CurrentSettings, primary, primary: true);
        // the Night Lantern clock follows the clock face picked in the panel and the dock's settings
        var clock = new ClockWindow(() => dock.CurrentSettings);
        var others = new Dictionary<string, (DockWindow dock, StripWindow strip)>();
        // the arrival conducts the start: everything starts out of sight and comes in, in order, once the garden is up
        Arrival.Init(dock, clock, () => new Window[] { dock, strip, clock }.Concat(others.Values.SelectMany(o => new Window[] { o.dock, o.strip })));
        Arrival.Hide();
        AudioMixer.Levels.Watch(); // each app's remembered level goes back on when it starts playing
        dock.Show(); strip.Show();
        dock.SettingsChanged += clock.Apply;
        // the clock, the gauges and the seal each move on their own (his rule: touching the clock must not move the
        // others); only "Reset to default" in the panel stacks them again
        Face = new FileSystemWatcher(@"C:\Users\Shadow\Documents\Rainmeter\Skins\Shrine\@Resources", "ClockSettings.inc") { EnableRaisingEvents = true };
        Face.Changed += (_, _) => app.Dispatcher.BeginInvoke(clock.Apply);
        dock.GameChanged += clock.Game;
        var snapAsk = new EventWaitHandle(false, EventResetMode.AutoReset, SnapshotEvent);
        new Thread(() => { while (true) { snapAsk.WaitOne(); app.Dispatcher.BeginInvoke(() => clock.Snapshot(System.IO.Path.Combine(AppContext.BaseDirectory, "clock-snapshot.png"))); } }) { IsBackground = true }.Start();
        // where he left the clock, gauges and seal survives refreshes, restarts and screen 2 coming and going
        var keeper = new WidgetKeeper(() => clock.Hwnd, (x, y) => Settings.Save(Settings.Load() with { ClockX = x, ClockY = y }));
        dock.DisplayChanged += keeper.ScreensChanged;

        // the other screens: each gets its own dock (its own bottom edge) and strip (its own top edge, its own windows)
        string? screensSeen = null;
        void Sync()
        {
            if (!dock.CurrentSettings.FreeScreen2 && Screens.KeepAcerLeft()) return; // moving it raises another display change
            var all = Screens.All();
            // when the screens really changed (one came, went or moved), Lively's layout goes stale (it keys screens by
            // DISPLAY number, which Shadow reshuffles): clean it and put the garden back on every screen
            var sig = string.Join(";", all.Select(s => $"{s.Device} {s.Area}"));
            if (screensSeen != null && sig != screensSeen)
            {
                // only a screen without a garden is fixed (asking Lively, or restarting it cleanly if its list is stale)
                Log($"screens: changed, checking the garden is on every screen ({sig})");
                _ = Arrival.EnsureGarden();
            }
            screensSeen = sig;
            var main = all.FirstOrDefault(s => s.Primary);
            if (main != null) { dock.MoveTo(main.Area); strip.MoveTo(main.Area); }
            var want = all.Where(s => !s.Primary).ToDictionary(s => $"{s.Device} {s.Area}", s => s.Area);
            foreach (var key in others.Keys.Except(want.Keys).ToList()) { others[key].dock.Close(); others[key].strip.Close(); others.Remove(key); Log($"screens: closed the dock and strip on {key}"); }
            foreach (var (key, area) in want.Where(w => !others.ContainsKey(w.Key)))
            {
                var d = new DockWindow(area, null); var st = new StripWindow(() => dock.CurrentSettings, area, primary: false);
                if (Arrival.IsHidden) d.Opacity = st.Opacity = 0;   // it comes in with the rest
                d.Show(); st.Show(); others[key] = (d, st); Log($"screens: a dock and strip on {key}");
            }
            // the clock window follows its settings; where the widgets sit after a screen change is the keeper's job
            clock.Apply();
        }
        var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        settle.Tick += (_, _) => { settle.Stop(); Sync(); };
        dock.DisplayChanged += () => { settle.Stop(); settle.Start(); Arrival.DisplayChanged(); };
        app.Dispatcher.BeginInvoke(Sync, DispatcherPriority.ApplicationIdle);
        app.Dispatcher.BeginInvoke(BorderKeeper.Start, DispatcherPriority.ApplicationIdle); // window edges in the Shrine look
        app.Dispatcher.BeginInvoke(() => _ = Arrival.Run(args.Contains("--quiet") ? "restart" : "sign-in", quiet: args.Contains("--quiet")), DispatcherPriority.ApplicationIdle);
        // our Start: the Win key opens it on the screen the mouse is on; the app list is read once early so it opens full
        void Follow() => WinKey.LetWindowsHaveIt = dock.CurrentSettings.WindowsStart || dock.InGame;
        Follow(); dock.SettingsChanged += Follow; dock.GameChanged += _ => Follow();
        WinKey.TaskKeys = () => app.Dispatcher.BeginInvoke(TasksPanel.Toggle); // Ctrl+Shift+Esc: Shrine Tasks (sheet 21)
        WinKey.Start(() => app.Dispatcher.BeginInvoke(DockWindow.StartWhereTheMouseIs));
        app.Dispatcher.BeginInvoke(() => AppCatalog.Refresh(app.Dispatcher), DispatcherPriority.ApplicationIdle);
        var tasksAsk = new EventWaitHandle(false, EventResetMode.AutoReset, TasksEvent);
        new Thread(() => { while (true) { tasksAsk.WaitOne(); app.Dispatcher.BeginInvoke(TasksPanel.Toggle); } }) { IsBackground = true }.Start();
        var arriveAsk = new EventWaitHandle(false, EventResetMode.AutoReset, ArriveEvent);
        new Thread(() => { while (true) { arriveAsk.WaitOne(); app.Dispatcher.BeginInvoke(() => { Arrival.Hide(); _ = Arrival.Run("asked (--arrive)"); }); } }) { IsBackground = true }.Start();
        app.Run();
    }
}

// Pinned: extra dock apps as "Name|exe path"; Hidden: built-in dock apps he unpinned (by key)
// StartPins/StartRecent: our Start's pins and last-opened apps, by name; WindowsStart: Win and the Start tile open
// Windows' own Start instead (missing reads as false, so ours is the default)
record Settings(bool HideUntilHover = true, int HideDelayMs = 900, int LabelDelayMs = 600, bool LockWidgets = false, string[]? Pinned = null, string[]? Hidden = null, int ClockX = 1488, int ClockY = 60, int ClockSize = 400, bool FreeScreen2 = false,
    string[]? StartPins = null, string[]? StartRecent = null, bool WindowsStart = false, bool ForgetLevels = false,
    string[]? Order = null)   // the dock's order after he drags (built-in keys, "pin:<launch>" for his pins)
{
    public static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShrineDock", "settings.json");
    public static Settings Load()
    {
        Settings s;
        try { s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new(); } catch { s = new(); }
        // a settings file from an older dock has no clock entries; those read as 0, so give them their defaults
        if (s.ClockSize <= 0) s = s with { ClockSize = 400 };
        if (s.ClockX <= 0 && s.ClockY <= 0) s = s with { ClockX = 1488, ClockY = 60 };
        return s;
    }
    public static void Save(Settings s)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
    }
}

record App(string Key, string Icon, int Size, bool Pixel, string Launch, string? LaunchArgs, string? Process, string Name = "", bool NewWindow = false);

class DockWindow : Window
{
    static readonly string Art = System.IO.Path.Combine(AppContext.BaseDirectory, "art");
    static readonly App[] BuiltIn =
    [
        new("start", "emblem.png", 44, false, "start", null, null, "Start"),
        new("files", "folder.png", 34, false, "explorer.exe", null, null, "File Explorer", true),
        new("wow", "rail-wow.png", 50, false, "wscript.exe", "\"C:\\Users\\Shadow\\Documents\\Rainmeter\\Skins\\BloodWired\\@Resources\\Scripts\\launch_app.vbs\" wow", "Wow", "WoW: Forever"),
        new("samara", "rail-samara.png", 64, true, @"C:\Users\Shadow\Desktop\Samara.lnk", null, "electron", "Samara"),
        new("claude", "icon-claude.png", 40, false, @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude", null, "claude", "Claude"),
        new("aang", "rail-aang.png", 54, true, @"C:\Users\Shadow\Desktop\Aang.lnk", null, "Aang", "Aang"),
        // Breath of the Wild in place of Cemu (his pick 2026-11-04): one click starts the game full screen
        new("botw", "icon-botw.png", 52, false, CemuExe, $"-f -g \"{BotW}\"", "Cemu", "Breath of the Wild"),
    ];
    // 85% of the first size (he found it a little large); Samara and Aang stay at their 1:1 pixel sizes
    const double Slot = 72, Gap = 10, SlabH = 94, LabelRoom = 70, Bottom = 14;

    readonly Canvas root = new();
    readonly Rectangle seam = new();
    readonly TranslateTransform rise = new();
    readonly List<(App app, Border slot, ScaleTransform scale, TranslateTransform lift, Border pip, FrameworkElement label)> tiles = [];
    App[] Apps = [];
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly DispatcherTimer hideTimer = new(), labelTimer = new(), openIntent = new() { Interval = TimeSpan.FromMilliseconds(80) };
    readonly DispatcherTimer running = new() { Interval = TimeSpan.FromSeconds(2) };
    Settings settings = Settings.Load();
    public Settings CurrentSettings => settings;
    public event Action? SettingsChanged;
    FileSystemWatcher? watcher; // a field, so it lives as long as the dock
    public event Action<bool>? GameChanged;
    public event Action? DisplayChanged;
    Rect area;
    bool open, asleepForGame;
    int hovered = -1;
    double winW, winH;
    Rect screen;

    public DockWindow(Rect area, EventWaitHandle? hello)
    {
        this.area = area;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ResizeMode = ResizeMode.NoResize; ShowActivated = false;
        Title = "Shrine Dock";
        Content = root; AllowDrop = true;
        Build();
        Loaded += (_, _) =>
        {
            Place(); SetClickThrough(true); Program.Log($"placed {Left},{Top} {Width}x{Height} screen {screen}");
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == 0x007E /*WM_DISPLAYCHANGE*/) DisplayChanged?.Invoke();
                return IntPtr.Zero;
            });
        };
        Live.Add(this);
        Closed += (_, _) => { Live.Remove(this); poll.Stop(); running.Stop(); hideTimer.Stop(); labelTimer.Stop(); openIntent.Stop(); watcher?.Dispose(); };
        poll.Tick += (_, _) => Track();
        poll.Start();
        hideTimer.Tick += (_, _) => { hideTimer.Stop(); if (!PointerOverDock()) Sink(); };
        labelTimer.Tick += (_, _) => { labelTimer.Stop(); if (hovered >= 0) Fade(tiles[hovered].label, 1, 150); };
        openIntent.Tick += (_, _) => { openIntent.Stop(); if (AtBottomEdge()) Rise(); };
        running.Tick += (_, _) => RefreshRunning();
        running.Start(); RefreshRunning();
        watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(Settings.Path)!, "settings.json") { EnableRaisingEvents = true };
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Settings.Path)!);
        watcher.Changed += (_, _) => Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(200); // let the writer finish, or the read sees a half-written file and falls back to defaults
            var old = settings; settings = Settings.Load();
            string Pins(Settings s) => string.Join("\n", s.Pinned ?? []) + "/" + string.Join("\n", s.Hidden ?? []) + "/" + string.Join("\n", s.Order ?? []);
            if (Pins(old) != Pins(settings)) Rebuild();
            if (!settings.HideUntilHover) Rise();
            SettingsChanged?.Invoke();
        });
        if (hello != null) new Thread(() => { while (true) { hello.WaitOne(); Dispatcher.BeginInvoke(SayHello); } }) { IsBackground = true }.Start();
        if (!settings.HideUntilHover) Dispatcher.BeginInvoke(Rise, DispatcherPriority.Loaded);
    }

    // the dock's apps: the built-ins he hasn't unpinned, then anything he pinned from the strip
    App[] CurrentApps()
    {
        var hidden = settings.Hidden ?? [];
        var list = BuiltIn.Where(a => !hidden.Contains(a.Key)).ToList();
        foreach (var p in settings.Pinned ?? [])
        {
            // "Name|exe", or from our Start "Name|shell:AppsFolder\id|icon.png" (an app id has no program file to read an icon from)
            var bits = p.Split('|'); if (bits.Length is < 2 or > 3) continue;
            bool fromStart = bits[1].StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
            list.Add(new("pin", bits.Length == 3 ? "file:" + bits[2] : "exe:" + bits[1], 40, false, bits[1], null, fromStart ? null : System.IO.Path.GetFileNameWithoutExtension(bits[1]), bits[0], !fromStart));
        }
        // his order (he drags them, 2026-11-04); anything not in it yet (a new pin) keeps its place at the end.
        // Start always stays first, as Finder does on the Mac.
        var order = settings.Order ?? [];
        if (order.Length > 0)
            list = [.. list.Select((a, i) => (a, i)).OrderBy(t => Array.IndexOf(order, KeyOf(t.a)) is var k and >= 0 ? k : order.Length + t.i).Select(t => t.a)];
        int st = list.FindIndex(a => a.Key == "start");
        if (st > 0) { var s = list[st]; list.RemoveAt(st); list.Insert(0, s); }
        return [.. list];
    }
    static string KeyOf(App a) => a.Key == "pin" ? "pin:" + a.Launch : a.Key;
    void Rebuild()
    {
        bool wasOpen = open; Hover(-1); open = false; dragFrom = -1; dragging = false;
        root.Children.Clear(); tiles.Clear(); Build(); Place(); RefreshRunning();
        if (wasOpen)
        {
            // already up: show the new row in place, without sinking and rising again
            open = true; SetClickThrough(false);
            rise.BeginAnimation(TranslateTransform.YProperty, null); rise.Y = 0;
            Group.Opacity = 1; seam.BeginAnimation(OpacityProperty, null); seam.Opacity = 0;
        }
        else SetClickThrough(true);
    }

    void Build()
    {
        Apps = CurrentApps();
        int n = Apps.Length, fi = Array.FindIndex(Apps, a => a.Key == "files");
        double slabW = n * Slot + (n - 1) * Gap + (fi >= 0 && fi < n - 1 ? Gap + 14 : 0); // the row of tiles, with the wider gap after File Explorer
        winW = slabW + 80; winH = SlabH + LabelRoom + Bottom + 20;
        Width = winW; Height = winH;

        var group = new Canvas { RenderTransform = rise };
        double x = (winW - slabW) / 2, slotTop = winH - Bottom - SlabH + (SlabH - Slot) / 2;
        centers.Clear(); this.slotTop = slotTop;
        for (int i = 0; i < n; i++)
        {
            if (fi >= 0 && i == fi + 1) x += Gap + 14; // a wider gap after File Explorer, no divider
            centers.Add(x + Slot / 2);
            group.Children.Add(MakeTile(i, x, slotTop));
            x += Slot + Gap;
        }
        root.Children.Add(group);

        // the seam you see while it sleeps
        seam.Width = 360; seam.Height = 4; seam.RadiusX = 2; seam.RadiusY = 2;
        seam.Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0x38, 0xC6, 0xFF));
        seam.Effect = new DropShadowEffect { Color = Color.FromRgb(0x38, 0xC6, 0xFF), BlurRadius = 12, ShadowDepth = 0, Opacity = .9 };
        Canvas.SetLeft(seam, (winW - 360) / 2); Canvas.SetTop(seam, winH - 5);
        root.Children.Add(seam);
        rise.Y = SlabH + Bottom + 30;
        group.Opacity = 0;
        group.Tag = "group";
    }

    UIElement MakeTile(int i, double x, double top)
    {
        var a = Apps[i];
        var host = new Canvas { Width = Slot, Height = Slot };
        Canvas.SetLeft(host, x); Canvas.SetTop(host, top);
        var scale = new ScaleTransform(1, 1, Slot / 2, Slot);
        var lift = new TranslateTransform();
        // each icon in its own small frame from the clock's family (sheet 08 v8)
        var slot = new Border
        {
            Width = Slot, Height = Slot, Background = TileBrush,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 4, Direction = 270, Opacity = .5 },
            RenderTransform = new TransformGroup { Children = { scale, lift } },
        };
        var img = new Image { Source = a.Icon.StartsWith("exe:") ? StripWindow.FromExe(a.Icon[4..]) : a.Icon.StartsWith("file:") ? LoadFile(a.Icon[5..]) : Load(a.Icon), Width = a.Size, Height = a.Size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(img, a.Pixel ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        slot.Child = img;
        var pip = new Border { Width = 14, Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0x38, 0xC6, 0xFF)), Opacity = 0,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x38, 0xC6, 0xFF), BlurRadius = 8, ShadowDepth = 0 } };
        Canvas.SetLeft(pip, (Slot - 14) / 2); Canvas.SetTop(pip, Slot + 7); // a soft rounded pip
        FrameworkElement label = a.Key == "pin" ? StripWindow.NamePlate(a.Name) : new Image { Source = Load($"label-{a.Key}.png"), Stretch = Stretch.None };
        label.Opacity = 0; label.IsHitTestVisible = false;
        label.Loaded += (_, _) => { Canvas.SetLeft(label, (Slot - label.ActualWidth) / 2); };
        Canvas.SetTop(label, -64);
        host.Children.Add(slot); host.Children.Add(pip); host.Children.Add(label);
        slot.MouseEnter += (_, _) => Hover(i);
        slot.MouseLeave += (_, _) => { if (hovered == i) Hover(-1); };
        // press and slide to move it (2026-11-04); a press that never slides is still a click. Start stays put.
        slot.MouseLeftButtonDown += (_, e) =>
        {
            if (a.Key == "start") return;
            dragFrom = i; dragTarget = -1; dragging = false; downAt = e.GetPosition(this); slot.CaptureMouse(); e.Handled = true;
        };
        slot.MouseMove += (_, e) =>
        {
            if (dragFrom != i || !slot.IsMouseCaptured) return;
            var p = e.GetPosition(this);
            if (!dragging && (Math.Abs(p.X - downAt.X) > 6 || Math.Abs(p.Y - downAt.Y) > 6)) { dragging = true; labelTimer.Stop(); Fade(label, 0, 100); }
            if (dragging) DragTo(p);
        };
        slot.MouseLeftButtonUp += (_, e) =>
        {
            bool was = dragging && dragFrom == i;
            ending = true; if (slot.IsMouseCaptured) slot.ReleaseMouseCapture(); ending = false;
            if (was) EndDrag(e.GetPosition(this), img.Source); else LaunchApp(a);
            dragFrom = -1; dragging = false;
        };
        slot.LostMouseCapture += (_, _) => { if (!ending && dragging && dragFrom == i) { dragFrom = -1; dragging = false; Rebuild(); } };
        slot.MouseRightButtonUp += (_, _) => Menu(a, img.Source);
        slot.Cursor = System.Windows.Input.Cursors.Hand;
        // files dropped on it: a program pins it here; anything else opens with this app (where the app can take it)
        slot.AllowDrop = true;
        slot.DragEnter += (_, e) => { dropFx = DropEffect(a, e); e.Effects = dropFx; e.Handled = true; Hover(i); };
        slot.DragOver += (_, e) => { e.Effects = dropFx; e.Handled = true; };
        slot.DragLeave += (_, _) => { if (hovered == i) Hover(-1); };
        slot.Drop += (_, e) => { e.Handled = true; Hover(-1); if (DroppedFiles(e) is { } f) Dropped(a, i, f); };
        tiles.Add((a, slot, scale, lift, pip, label));
        return host;
    }

    // ---- dragging a tile along the row (2026-11-04): the others step aside; pulled up and off, it asks to unpin
    int dragFrom = -1, dragTarget = -1;
    bool dragging, ending;
    Point downAt;
    double slotTop;
    readonly List<double> centers = [];
    const double OffBy = 70;   // pulled this far above the row, letting go asks to unpin
    void DragTo(Point p)
    {
        int from = dragFrom; var me = tiles[from];
        me.lift.BeginAnimation(TranslateTransform.XProperty, null); me.lift.BeginAnimation(TranslateTransform.YProperty, null);
        // it follows the mouse up only as far as the dock's own window reaches, or it would vanish past its top edge
        me.lift.X = p.X - downAt.X; me.lift.Y = Math.Max(-slotTop, -8 + Math.Min(0, p.Y - downAt.Y));
        bool off = p.Y < slotTop - OffBy;
        me.slot.Opacity = off ? .45 : 1;
        int t = from;
        if (!off)
        {
            double c = centers[from] + p.X - downAt.X; t = 0;
            for (int j = 0; j < centers.Count; j++) if (j != from && centers[j] < c) t++;
            if (tiles.Count > 0 && tiles[0].app.Key == "start") t = Math.Max(t, 1);   // Start stays first
        }
        if (t == dragTarget) return;
        dragTarget = t;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        for (int j = 0; j < tiles.Count; j++)
        {
            if (j == from) continue;
            int end = j < from ? (j >= t ? j + 1 : j) : (j <= t ? j - 1 : j);
            tiles[j].lift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(centers[end] - centers[j], TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        }
    }
    void EndDrag(Point p, ImageSource? icon)
    {
        int from = dragFrom, t = dragTarget < 0 ? dragFrom : dragTarget; var a = tiles[from].app;
        if (p.Y < slotTop - OffBy)
        {
            Rebuild();   // back in its place while the question is up
            GetCursorPos(out var c); var d = VisualTreeHelper.GetDpi(this);
            ShrineMenu.Confirm($"Unpin {a.Name} from the dock?", icon, "Yes, unpin", () => Unpin(a), new Point(c.X / d.DpiScaleX, c.Y / d.DpiScaleY), upward: true);
            return;
        }
        if (t == from) { Rebuild(); return; }
        var keys = Apps.Select(KeyOf).ToList(); var k = keys[from]; keys.RemoveAt(from); keys.Insert(t, k);
        settings = Settings.Load() with { Order = [.. keys] }; Settings.Save(settings);
        Rebuild();
    }

    // ---- files dragged onto a tile
    DragDropEffects dropFx;
    static string[]? DroppedFiles(DragEventArgs e) => e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;
    /// <summary>A shortcut's target (an app shortcut with no file target stays the shortcut).</summary>
    static string Resolve(string f)
    {
        if (!f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return f;
        try { dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; string t = sh.CreateShortcut(f).TargetPath; return string.IsNullOrEmpty(t) ? f : t; }
        catch { return f; }
    }
    static bool IsProgram(string f) { var r = Resolve(f); return !Directory.Exists(r) && (r.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || r.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)); }
    // only apps that take a file to open: File Explorer, and his own pins that are plain programs
    static bool OpensFiles(App a) => a.Key == "files" || (a.Key == "pin" && a.Launch.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    static DragDropEffects DropEffect(App a, DragEventArgs e)
    {
        var f = DroppedFiles(e);
        if (f == null || f.Length == 0) return DragDropEffects.None;
        if (f.All(IsProgram)) return DragDropEffects.Link;
        return OpensFiles(a) ? DragDropEffects.Copy : DragDropEffects.None;
    }
    void Dropped(App a, int at, string[] files)
    {
        if (files.All(IsProgram)) { PinHere(files, at); return; }
        if (!OpensFiles(a)) return;
        foreach (var f in files.Take(10))
            if (a.Key == "files") Run("explorer.exe", Directory.Exists(f) ? $"\"{f}\"" : $"/select,\"{f}\"");
            else Run(a.Launch, $"\"{f}\"", System.IO.Path.GetDirectoryName(a.Launch));
    }
    /// <summary>Programs dropped on the dock join it right after the tile they landed on.</summary>
    void PinHere(string[] files, int at)
    {
        var s = Settings.Load(); var pins = (s.Pinned ?? []).ToList(); var keys = Apps.Select(KeyOf).ToList(); int pos = at + 1;
        foreach (var f in files)
        {
            var exe = Resolve(f);
            if (IsPinned(exe, out _) || pins.Any(p => p.Split('|') is var b && b.Length >= 2 && b[1].Equals(exe, StringComparison.OrdinalIgnoreCase))) continue;
            pins.Add(System.IO.Path.GetFileNameWithoutExtension(f).Replace("|", " ") + "|" + exe);
            keys.Insert(Math.Min(pos++, keys.Count), "pin:" + exe);
        }
        Settings.Save(s with { Pinned = [.. pins], Order = [.. keys] });
    }

    static BitmapImage? LoadFile(string path)
    {
        try { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); return b; }
        catch { return null; }
    }
    static BitmapImage Load(string file)
    {
        var b = new BitmapImage();
        b.BeginInit(); b.UriSource = new Uri(System.IO.Path.Combine(Art, file)); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze();
        return b;
    }

    public void MoveTo(Rect a) { if (a == area) return; area = a; Place(); }
    void Place()
    {
        screen = area;
        Left = screen.Left + (screen.Width - winW) / 2;
        Top = screen.Bottom - winH;
    }

    // ---- where the mouse is
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    struct POINT { public int X, Y; }
    Point MousePos() { GetCursorPos(out var p); var d = VisualTreeHelper.GetDpi(this); return new Point(p.X / d.DpiScaleX, p.Y / d.DpiScaleY); }
    bool AtBottomEdge() { var c = MousePos(); return c.Y >= screen.Bottom - 3 && c.X >= Left + 40 && c.X <= Left + winW - 40; }
    bool PointerOverDock()
    {
        var c = MousePos();
        double slabTop = Top + winH - Bottom - SlabH - 40; // a little room above for the lifted icon and its name
        return c.X >= Left + 30 && c.X <= Left + winW - 30 && c.Y >= slabTop && c.Y <= screen.Bottom;
    }

    int gameCheck;
    void Track()
    {
        if (++gameCheck >= 15) { gameCheck = 0; CheckGame(); }   // half a second, so alt-tab out brings it back quickly
        if (asleepForGame) return;
        if (!open) { if (AtBottomEdge()) { if (!openIntent.IsEnabled) openIntent.Start(); } else openIntent.Stop(); return; }
        if (dragging) { hideTimer.Stop(); return; }   // never sink under a drag, even one pulled up and off
        if (!settings.HideUntilHover) return;
        if (PointerOverDock()) hideTimer.Stop();
        else if (!hideTimer.IsEnabled) { hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(150, settings.HideDelayMs)); hideTimer.Start(); }
    }

    // ---- rise and sink: one eased slide on the GPU
    Canvas Group => (Canvas)root.Children[0];
    void Rise()
    {
        if (open || asleepForGame) return;
        open = true; SetClickThrough(false);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        Fade(Group, 1, 200); Fade(seam, 0, 150);
    }
    void Sink()
    {
        if (!open) return;
        open = false; Hover(-1);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SlabH + Bottom + 30, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        Fade(Group, 0, 260); Fade(seam, 1, 300);
        SetClickThrough(true);
    }
    internal void SayHello() { Rise(); var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) }; t.Tick += (_, _) => { t.Stop(); if (!PointerOverDock() && settings.HideUntilHover) Sink(); }; t.Start(); }

    // ---- only the icon under the mouse reacts: one smooth lift, then it settles
    void Hover(int i)
    {
        if (i == hovered) return;
        if (hovered >= 0) { var o = tiles[hovered]; Grow(o.scale, o.lift, 1, 0); Fade(o.label, 0, 120); Rim(o.slot, false); }
        hovered = i; labelTimer.Stop();
        if (i < 0) return;
        var t = tiles[i];
        Grow(t.scale, t.lift, 1.15, -8); Rim(t.slot, true);
        if (settings.LabelDelayMs >= 0) { labelTimer.Interval = TimeSpan.FromMilliseconds(settings.LabelDelayMs); labelTimer.Start(); }
    }
    static void Grow(ScaleTransform s, TranslateTransform l, double to, double y)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut }; var d = TimeSpan.FromMilliseconds(200);
        s.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, d) { EasingFunction = ease });
        s.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, d) { EasingFunction = ease });
        l.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, d) { EasingFunction = ease });
    }
    static readonly ImageBrush TileBrush = Frozen(new ImageBrush(Load("dock-tile.png"))), TileHotBrush = Frozen(new ImageBrush(Load("dock-tile-hot.png")));
    static ImageBrush Frozen(ImageBrush b) { b.Freeze(); return b; }
    static void Rim(Border b, bool on) => b.Background = on ? TileHotBrush : TileBrush;
    static void Fade(UIElement e, double to, int ms) => e.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));

    // ---- clicks go through while it sleeps
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    void SetClickThrough(bool through)
    {
        var h = new WindowInteropHelper(this).Handle; if (h == IntPtr.Zero) return;
        int ex = GetWindowLong(h, -20) | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/;
        ex = through ? ex | 0x20 /*TRANSPARENT*/ : ex & ~0x20;
        SetWindowLong(h, -20, ex);
    }

    // ---- apps
    void LaunchApp(App a, bool fresh = false)
    {
        try
        {
            if (a.Launch == "start") { PressStart(); return; }
            // already open: bring its window up straight away, instead of starting the app again just to hand over
            // (2026-11-02, he found Samara slow to come up from the dock)
            var win = fresh || a.Process == null ? IntPtr.Zero : StripWindow.WindowOf(a.Process, a.Key == "samara" ? "Samara" : null);
            if (win != IntPtr.Zero) { StripWindow.Focus(win); return; }
            Process.Start(new ProcessStartInfo(a.Launch, a.LaunchArgs ?? "") { UseShellExecute = true });
        }
        catch { /* a missing shortcut should never take the dock down */ }
    }
    // right-click: the Shrine menu for this dock app
    void Menu(App a, ImageSource? icon)
    {
        var items = new List<MenuEntry?>();
        if (a.Key == "start") { items.Add(new("Open Start", () => StartMenu.Toggle(screen, StartTileX()))); items.Add(new("Windows' Start", WindowsStart)); }
        GetCursorPos(out var c); var d = VisualTreeHelper.GetDpi(this);
        var at = new Point(c.X / d.DpiScaleX, c.Y / d.DpiScaleY);
        if (a.Key != "start")
        {
            // the app's own things first (his picks, 2026-11-03), as Windows' jump lists put tasks above the windows
            if (AppItems(a, items, icon, at)) items.Add(null);
            // his picks (2026-11-03): its open windows by name when there are several, move to the other screen, close all
            // (Aang's own windows are tool windows, not app windows: his items above stand for them)
            var wins = a.Process == null || a.Key == "aang" ? [] : StripWindow.WindowsOf(a.Process, a.Key == "samara" ? "Samara" : null);
            if (wins.Count > 0) StripWindow.AddWindowItems(items, wins);
            else if (!(a.Key == "aang" && AangRunning)) items.Add(new("Open", () => LaunchApp(a)));
            if (a.NewWindow) items.Add(new("New window", () => LaunchApp(a, true)));
            StripWindow.AddCloseItems(items, wins);
            items.Add(null);
            // he unpinned by accident once: it asks first
            items.Add(new("Unpin from dock", () => ShrineMenu.Confirm($"Unpin {a.Name} from the dock?", icon, "Yes, unpin", () => Unpin(a), at, upward: true)));
        }
        ShrineMenu.Open(a.Name, icon, items, at, upward: true);
    }
    // ---- each app's own right-click items (his picks, 2026-11-03)
    const string AangExe = @"C:\Users\Shadow\Documents\AangApp\src\Body\bin\Release\net10.0-windows\Aang.exe";
    const string AangLink = @"C:\Users\Shadow\Desktop\Aang.lnk";
    const string ShellDir = @"C:\Users\Shadow\Documents\AangApp\src\Shell";
    const string CemuExe = @"C:\Games\Cemu\Cemu.exe";
    const string BotW = @"C:\Games\Cemu\portable\mlc01\usr\title\00050000\101c9400\code\U-King.rpx";
    static bool AangRunning => System.Diagnostics.Process.GetProcessesByName("Aang").Length > 0;
    /// <summary>Adds the app's own items; true when it added any.</summary>
    bool AppItems(App a, List<MenuEntry?> items, ImageSource? icon, Point at)
    {
        int before = items.Count;
        switch (a.Key)
        {
            // Aang: his tray's Show or hide, and restart or quit (both ask first), through Aang.exe's door (--pet-*)
            case "aang" when AangRunning:
                items.Add(new("Show or hide Aang", () => Run(AangExe, "--pet-toggle")));
                items.Add(new("Restart Aang", () => ShrineMenu.Confirm("Restart Aang?", icon, "Yes, restart", () =>
                {
                    Run(AangExe, "--pet-quit");
                    Task.Run(() =>
                    {
                        for (int i = 0; i < 40 && AangRunning; i++) Thread.Sleep(250);
                        Run(AangLink, "");
                    });
                }, at, upward: true)));
                items.Add(new("Quit Aang", () => ShrineMenu.Confirm("Quit Aang?", icon, "Yes, quit", () => Run(AangExe, "--pet-quit"), at, upward: true), Warn: true));
                break;
            // Samara: its own taskbar list, through the same door (a second Shell hands --samara-task to the running one)
            case "samara":
                var shell = SamaraEngine();
                if (shell == null) break;
                void Do(string label, string t) => items.Add(new(label, () => Run(shell, $"src/main.ts --samara-task={t}", ShellDir)));
                Do("New tab", "new-tab"); Do("New window", "new-window"); Do("Mockups", "mockups"); Do("Hyrule", "hyrule"); Do("Settings", "settings");
                items.Add(null);
                Do("Show all pop-outs", "popouts-show"); Do("Hide all pop-outs", "popouts-hide");
                if (AangRunning) { Do("New note", "new-note"); Do("Timer", "timer"); Do("Music player", "player"); }
                break;
            // Breath of the Wild: Cemu on its own, for settings and other games
            case "botw" when System.Diagnostics.Process.GetProcessesByName("Cemu").Length == 0:
                items.Add(new("Open Cemu only", () => Run(CemuExe, "", System.IO.Path.GetDirectoryName(CemuExe))));
                break;
        }
        return items.Count > before;
    }
    /// <summary>The engine the running Samara was started with (its tasks must go to that same Shell), or null.</summary>
    static string? SamaraEngine()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher("SELECT ExecutablePath, CommandLine FROM Win32_Process WHERE Name='electron.exe'");
            foreach (var o in q.Get())
            {
                var cmd = o["CommandLine"]?.ToString() ?? "";
                if (cmd.Contains("main.ts") && !cmd.Contains("--type=")) return o["ExecutablePath"]?.ToString();
            }
        }
        catch (Exception e) { Program.Log("dock: samara engine " + e.Message); }
        return null;
    }
    static void Run(string exe, string args, string? dir = null)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = exe.EndsWith(".lnk"), CreateNoWindow = true, WorkingDirectory = dir ?? "" }); }
        catch (Exception e) { Program.Log($"dock: {System.IO.Path.GetFileName(exe)} {args}: {e.Message}"); }
    }

    static void Unpin(App a)
    {
        var s = Settings.Load();
        Settings.Save(a.Key == "pin" ? s with { Pinned = (s.Pinned ?? []).Where(p => p.Split('|') is var b && (b.Length < 2 || b[1] != a.Launch)).ToArray() } : s with { Hidden = [.. (s.Hidden ?? []), a.Key] });
    }
    public static bool IsPinned(string exe, out string? builtInKey)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(exe); var s = Settings.Load();
        builtInKey = BuiltIn.FirstOrDefault(b => b.Process != null && b.Process.Equals(name, StringComparison.OrdinalIgnoreCase))?.Key;
        if (builtInKey != null) return !(s.Hidden ?? []).Contains(builtInKey);
        return (s.Pinned ?? []).Any(p => p.EndsWith("|" + exe, StringComparison.OrdinalIgnoreCase));
    }

    // ---- Start: ours opens on this dock's screen, above its Start tile; Windows' own only if he chose it
    static readonly List<DockWindow> Live = [];
    public bool InGame => asleepForGame;
    double StartTileX()
    {
        var t = tiles.FirstOrDefault(t => t.app.Key == "start").slot;
        if (t == null) return screen.Left + screen.Width / 2;
        return t.TranslatePoint(new Point(Slot / 2, 0), this).X + Left;
    }
    void PressStart() { if (settings.WindowsStart) WindowsStart(); else StartMenu.Toggle(screen, StartTileX()); }
    /// <summary>The Win key: our Start on the screen the mouse is on, above that screen's dock.</summary>
    public static void StartWhereTheMouseIs()
    {
        GetCursorPos(out var c);
        var dock = Live.FirstOrDefault(d => d.screen.Contains(new Point(c.X, c.Y))) ?? Live.FirstOrDefault();
        if (dock != null) StartMenu.Toggle(dock.screen, dock.StartTileX());
    }
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void WindowsStart() { keybd_event(0x5B, 0, 0, WinKey.Mine); keybd_event(0x5B, 0, 2, WinKey.Mine); }

    void RefreshRunning()
    {
        foreach (var t in tiles)
        {
            bool on = t.app.Process != null && System.Diagnostics.Process.GetProcessesByName(t.app.Process).Length > 0;
            t.pip.Opacity = on ? 1 : 0;
        }
    }

    // ---- games, like Windows (his pick 2026-11-02): out of sight while WoW or Cemu is the window in front on this
    // screen; alt-tab out, or use the other screen, and the dock is back
    void CheckGame()
    {
        bool game = StripWindow.GameInFrontOn(screen);
        if (game == asleepForGame) return;
        asleepForGame = game; GameChanged?.Invoke(game);
        if (game) { Sink(); Visibility = Visibility.Hidden; running.Stop(); }
        else { Visibility = Visibility.Visible; running.Start(); SetClickThrough(true); if (!settings.HideUntilHover) Rise(); }
    }
}
